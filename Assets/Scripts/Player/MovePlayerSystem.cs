using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Aspects;
using Unity.Physics.Extensions;
using Unity.Transforms;
using UnityEngine;
using CapsuleCollider = Unity.Physics.CapsuleCollider;

public struct NoClip : IComponentData { }

[UpdateBefore(typeof(TransformSystemGroup))]
public partial class MovePlayerSystem : SystemBase
{
    CollisionFilter _collisionFilter;
    protected override void OnCreate()
    {
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<CameraAnchor>();
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

        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        var collisionWorld = physicsWorld.CollisionWorld;

        var p = SystemAPI.GetComponentRW<Player>(localPlayer);
        p.ValueRW.Running = inputData.WantRun;
        var movementSpeed = p.ValueRW.Running ? p.ValueRW.MovementSpeedRun : p.ValueRW.MovementSpeedDefault;

        var deltaTime = World.Time.DeltaTime;

        var playerIsRefFrame = SystemAPI.HasComponent<ReferenceFrameOrigin>(localPlayer);

        var frameToCamera = playerIsRefFrame 
            ? localToCamera.Rotation 
            : math.mul(math.inverse(referenceFrame.PreviousLtw.Rotation), localToCamera.Rotation);
        var movement = math.mul(frameToCamera, inputData.Movement);
        var totalMovement = movement * movementSpeed * deltaTime;
        var transform = SystemAPI.GetComponentRW<LocalTransform>(localPlayer);

        if (math.any(movement != 0) && !SystemAPI.HasComponent<NoClip>(localPlayer))
        {
            var colliderAspect = SystemAPI.GetAspect<ColliderAspect>(localPlayer);
            var direction = math.normalize(movement);
            var distance = math.length(totalMovement);
            if(collisionWorld.CastCollider(colliderAspect, direction, distance, out var hit))
                totalMovement *= hit.Fraction;
        }

        if (playerIsRefFrame)
        {
            var targetRotation = math.mul(transform.ValueRW.Rotation, quaternion.EulerXYZ(rotation));
            transform.ValueRW.Rotation = targetRotation;
            var ltg = SystemAPI.GetComponentRW<LocalToGalaxy>(localPlayer);
            ltg.ValueRW += totalMovement;
        }
        else
        {
            transform.ValueRW.Position += totalMovement;
            var targetRotation = math.mul(transform.ValueRW.Rotation, quaternion.EulerXYZ(rotation));
            var targetRotationUpAligned =
                quaternion.LookRotationSafe(math.mul(targetRotation, math.forward()), math.up());
            transform.ValueRW.Rotation = math.slerp(targetRotation, targetRotationUpAligned, alignUpVector);
        }
    }
}