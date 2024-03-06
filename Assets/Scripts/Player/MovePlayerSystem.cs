using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Aspects;
using Unity.Transforms;
using UnityEngine;

public struct NoClip : IComponentData { }

[UpdateBefore(typeof(TransformSystemGroup))]
public partial class MovePlayerSystem : SystemBase
{
    CollisionFilter _collisionFilter;
    protected override void OnCreate()
    {
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<ControlScheme.Player>();
        RequireForUpdate<InputData>();
        RequireForUpdate<PhysicsWorldSingleton>();
        RequireForUpdate<ReferenceFrame>();
        
        _collisionFilter = CollisionFilter.Default;
        _collisionFilter.CollidesWith &= ~(1u<<3);
    }

    protected override unsafe void OnUpdate()
    {
        var localPlayer = SystemAPI.GetSingletonEntity<LocalPlayer>();
        var referenceFrame = SystemAPI.GetSingleton<ReferenceFrame>();
        var camOrientation = SystemAPI.GetSingletonRW<CameraSettings>();
        var inputData = SystemAPI.GetSingleton<InputData>();
        var alignUpVector = math.min(referenceFrame.DurationSinceLastChange / 2, 1);

        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        var collisionWorld = physicsWorld.CollisionWorld;

        var p = SystemAPI.GetComponentRW<Player>(localPlayer);
        p.ValueRW.Running = inputData.WantRun;
        var movementSpeed = p.ValueRW.Running ? p.ValueRW.MovementSpeedRun : p.ValueRW.MovementSpeedDefault;

        var deltaTime = World.Time.DeltaTime;

        var playerIsRefFrame = referenceFrame.Origin == localPlayer;
        var transform = SystemAPI.GetComponentRW<LocalTransform>(localPlayer);
        var appliedRotationChange = camOrientation.ValueRW.Orientation;

        var updatedRotationFull = math.mul(transform.ValueRW.Rotation, appliedRotationChange);
        if (!playerIsRefFrame)
        {
            var euler = math.Euler(updatedRotationFull);
            var camYaw = quaternion.Euler(0, euler.y, 0); // Camera rotation with only yaw
            var camPitch = quaternion.Euler(euler.x, 0, 0); // Camera rotation with only pitch

            transform.ValueRW.Rotation = math.slerp(transform.ValueRW.Rotation, camYaw, alignUpVector);
            appliedRotationChange = camPitch;
        }
        else
        {
            transform.ValueRW.Rotation = updatedRotationFull;
            appliedRotationChange = quaternion.identity;
        }
        camOrientation.ValueRW.Orientation = appliedRotationChange;

        var movement = math.mul(transform.ValueRW.Rotation, inputData.Movement);
        var totalMovement = movement * movementSpeed * deltaTime;

        if (math.any(movement != 0) && !SystemAPI.HasComponent<NoClip>(localPlayer))
        {
            var colliderAspect = SystemAPI.GetAspect<ColliderAspect>(localPlayer);
            var direction = math.normalize(movement);
            var distance = math.length(totalMovement);
            if (collisionWorld.CastCollider(colliderAspect, direction, distance, out var hit))
            {
                var slide = math.reflect(direction, hit.SurfaceNormal);
                var angleDifference = Vector3.Angle(slide, direction) / 180;
                var dampingFactor = math.lerp(1f, 0f, math.clamp(angleDifference, 0, 1)); // Adjust the range as needed
                totalMovement = slide * distance * dampingFactor;
            }
        }

        if (playerIsRefFrame)
        {
            var ltg = SystemAPI.GetComponentRW<LocalToGalaxy>(localPlayer);
            ltg.ValueRW += totalMovement;
        }
        else
        {
            transform.ValueRW.Position += totalMovement;
        }
    }
}