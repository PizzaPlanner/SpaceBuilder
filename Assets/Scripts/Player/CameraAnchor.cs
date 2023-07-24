using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

public struct CameraAnchor : IComponentData
{
    public float ScrollFactor;
    public float3 MaxOffset;
    public float XRotation;

    public float3 CameraPosition;
    public float3 CameraForward;

    public uint CollidesWith;
}


[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class CameraControllerSystem : SystemBase
{
    float3 camDirection;
    CollisionFilter filter;

    protected override void OnCreate()
    {
        RequireForUpdate<CameraAnchor>();
        RequireForUpdate<ReferenceFrame>();
        filter = CollisionFilter.Default;
        filter.BelongsTo = ~0u;
    }

    protected override void OnUpdate()
    {
        var anchorLtw = SystemAPI.GetComponent<LocalToWorld>(SystemAPI.GetSingletonEntity<CameraAnchor>());
        var anchor = SystemAPI.GetSingletonRW<CameraAnchor>();

        var camDistance = anchor.ValueRO.ScrollFactor;
        var rotatedOffset = math.mul(anchorLtw.Rotation, anchor.ValueRO.MaxOffset);
        filter.CollidesWith = anchor.ValueRO.CollidesWith;
        RaycastInput raycastInput = new RaycastInput
        {
            Start = anchorLtw.Position,
            End = anchorLtw.Position + rotatedOffset,
            Filter = filter
        };
        if (SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorld) && 
            physicsWorld.PhysicsWorld.CollisionWorld.CastRay(raycastInput, out var raycastHit))
        {
            camDistance *= raycastHit.Fraction - 0.1f;
        }

        var newPosition = anchorLtw.Position + rotatedOffset * camDistance;
        var camTransform = Camera.main.transform;
        camTransform.position = newPosition;
        camTransform.rotation = anchorLtw.Rotation;
        camTransform.RotateAround(anchorLtw.Position, anchorLtw.Right, anchor.ValueRO.XRotation );
        anchor.ValueRW.CameraForward = camTransform.forward;
        anchor.ValueRW.CameraPosition = newPosition;
    }
}
