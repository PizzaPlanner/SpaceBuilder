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
        var referenceFrame = SystemAPI.GetSingleton<ReferenceFrame>();
        var camera = SystemAPI.GetSingletonEntity<CameraAnchor>();
        var localToCamera = SystemAPI.GetComponent<LocalToWorld>(camera);
        var inputData = SystemAPI.GetSingleton<InputData>();
        var alignUpVector = math.min(referenceFrame.DurationSinceLastChange / 3, 1);

        // --- Rotation ---
        var rotation = inputData.Rotation;

        var camAnchor = SystemAPI.GetSingleton<CameraAnchor>();
        if (SystemAPI.HasComponent<Parent>(localPlayer))
        {
            camAnchor.XRotation += math.degrees(rotation.x);
            camAnchor.XRotation = math.clamp(camAnchor.XRotation, -90, 75);
            rotation.z = 0;
        }
        else
            camAnchor.XRotation = 0;
        SystemAPI.SetSingleton(camAnchor);

        var p = SystemAPI.GetComponentRW<Player>(localPlayer);
        p.ValueRW.Running = inputData.WantRun;

        var deltaTime = World.Time.DeltaTime;

        Entities
            .WithAll<ReferenceFrameOrigin>()
            .ForEach((Entity e, ref LocalTransform transform, ref CanTravelSectors cts, ref LocalToGalaxy ltg, in Player player) =>
            {
                var movement = math.mul(localToCamera.Rotation, inputData.Movement);

                var targetRotation = math.mul(transform.Rotation, quaternion.EulerXYZ(rotation));
                transform.Rotation = targetRotation;
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
                var frameToCamera = math.mul(math.inverse(referenceFrame.PreviousLtw.Rotation), localToCamera.Rotation);
                var movement = math.mul(frameToCamera, inputData.Movement);
                
                var totalMovement = (movement * (player.Running ? player.MovementSpeedRun : player.MovementSpeedDefault) * deltaTime);
                transform.Position += totalMovement;
                var targetRotation = math.mul(transform.Rotation, quaternion.EulerXYZ(rotation));
                var targetRotationUpAligned =
                    quaternion.LookRotationSafe(math.mul(targetRotation, math.forward()), math.up());
                transform.Rotation = math.slerp(targetRotation, targetRotationUpAligned, alignUpVector);
            }).Schedule();
    }
}