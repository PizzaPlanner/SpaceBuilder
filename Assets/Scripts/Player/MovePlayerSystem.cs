using Galaxy.SectorLoading;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateBefore(typeof(TransformSystemGroup))]
public partial class MovePlayerSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<CameraAnchor>();
        RequireForUpdate<ControlScheme.Player>();
        RequireForUpdate<InputData>();
    }

    protected override void OnUpdate()
    {
        var localPlayer = SystemAPI.GetSingletonEntity<LocalPlayer>();
        var camera = SystemAPI.GetSingletonEntity<CameraAnchor>();
        var cameraLtw = SystemAPI.GetComponent<LocalToWorld>(camera);
        var inputData = SystemAPI.GetSingleton<InputData>();
        
        // --- Movement ---
        var movement = inputData.Movement;
        movement = math.mul(cameraLtw.Rotation, movement);

        // --- Rotation ---
        var rotation = inputData.Rotation;

        var camAnchor = SystemAPI.GetSingleton<CameraAnchor>();
        if (SystemAPI.HasComponent<Docked>(localPlayer))
        {
            camAnchor.XRotation += math.degrees(rotation.x);
            camAnchor.XRotation = math.clamp(camAnchor.XRotation, -90, 75);
        }
        else
            camAnchor.XRotation = 0;
        SystemAPI.SetSingleton(camAnchor);

        var p = SystemAPI.GetComponentRW<Player>(localPlayer);
        p.ValueRW.Running = inputData.WantRun;

        var deltaTime = World.Time.DeltaTime;
        
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(World.Unmanaged);
        Entities
            .WithAll<ReferenceFrameOrigin>()
            .ForEach((Entity e, ref LocalTransform transform, ref CanTravelSectors cts, ref LocalToGalaxy ltg, ref Player player) =>
            {
                transform.Rotation = math.mul(transform.Rotation, quaternion.EulerXYZ(rotation));
                var totalMovement = (movement * (double3)(player.Running ? player.MovementSpeedRun : player.MovementSpeedDefault) * deltaTime);
                ltg += totalMovement;

                cts.CurrentTravelType = player.Running && player.MovementSpeedRun > 1e9
                    ? SectorChangeType.FTL
                    : SectorChangeType.Normal;

                var relativePos = (cts.PreviousSector - ltg).Distance;
                cts.CurrentSpeed = math.length(relativePos) / deltaTime;
            }).Schedule();

        Entities
            .WithNone<ReferenceFrameOrigin>()
            .ForEach((ref LocalTransform transform, in Player player) =>
            {
                var totalMovement = (movement * (player.Running ? player.MovementSpeedRun : player.MovementSpeedDefault) * deltaTime);
                transform.Position += totalMovement;
                transform.Rotation = math.mul(transform.Rotation, quaternion.EulerXYZ(rotation));
            }).Schedule();
    }
}