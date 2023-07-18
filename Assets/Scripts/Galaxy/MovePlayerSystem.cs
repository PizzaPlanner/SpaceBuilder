using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateBefore(typeof(TransformSystemGroup))]
public partial class MovePlayerSystem : SystemBase
{
    private const float ROTATION_SENSIVITY = 0.2f;

    protected override void OnCreate()
    {
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<CameraAnchor>();
        RequireForUpdate<InputSystem.ControlPlayer>();
        RequireForUpdate<ControlScheme.OnFeet>();
    }

    protected override void OnUpdate()
    {
        var localPlayer = SystemAPI.GetSingletonEntity<LocalPlayer>();
        var camera = SystemAPI.GetSingletonEntity<CameraAnchor>();
        var cameraLtw = SystemAPI.GetComponent<LocalToWorld>(camera);

        // --- Movement ---
        var movement = new float3(Input.GetAxis("MoveXAxis"), Input.GetAxis("MoveYAxis"), Input.GetAxis("MoveZAxis"));
        // Normalized * Speed
        movement = math.normalizesafe(movement);
        // Local -> World
        movement = math.mul(cameraLtw.Rotation, movement);

        // --- Rotation ---
        var rotation =  new float3(-Input.GetAxis("Mouse Y"), Input.GetAxis("Mouse X"), Input.GetAxis("RotateZAxis") * 5) * ROTATION_SENSIVITY;

        var camAnchor = SystemAPI.GetSingleton<CameraAnchor>();
        if (SystemAPI.HasComponent<Docked>(localPlayer))
        {
            camAnchor.XRotation += math.degrees(rotation.x);
            camAnchor.XRotation = math.clamp(camAnchor.XRotation, -90, 75);
        }
        else
            camAnchor.XRotation = 0;
        SystemAPI.SetSingleton(camAnchor);

        var player = SystemAPI.GetComponent<Player>(localPlayer);
        player.Running = Input.GetKey(KeyCode.LeftShift);
        SystemAPI.SetComponent(localPlayer, player);

        var deltaTime = World.Time.DeltaTime;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        Entities
            .WithNone<Docked>()
            .ForEach((Entity e, ref LocalTransform transform, ref CanTravelSectors cts, ref LocalToGalaxy ltg, ref Player player) =>
            {
                transform.Rotation = math.mul(transform.Rotation, quaternion.EulerXYZ(rotation));
                var totalMovement = (movement * (double3)(player.Running ? player.MovementSpeedRun : player.MovementSpeedDefault) * deltaTime);
                ltg += totalMovement;

                cts.CurrentTravelType = player.Running && player.MovementSpeedRun > 1e9
                    ? SectorChangeType.FTL
                    : SectorChangeType.Normal;
                if (cts.PreviousSector != ltg)
                {
                    var update = ecb.CreateEntity();
                    ecb.AddComponent(update, new SectorChange
                    {
                        Entity = e,
                        Previous = cts.PreviousSector,
                        New = ltg,
                        ChangeType = cts.CurrentTravelType
                    });
                }

                var relativePos = (cts.PreviousSector - ltg).Distance;
                cts.CurrentSpeed = math.length(relativePos) / deltaTime;
            }).Schedule();

        Entities
            .ForEach((ref Docked docked, in Player player) =>
            {
                var movementInversedByParent = math.mul(math.inverse(docked.TargetRotation), movement); 
                docked.Rotation = math.mul(docked.Rotation, quaternion.EulerXYZ(new float3(0, rotation.y, 0)));
                docked.Position += movementInversedByParent * (player.Running ? player.MovementSpeedRun : player.MovementSpeedDefault) * deltaTime;
            }).Schedule();
    }
}