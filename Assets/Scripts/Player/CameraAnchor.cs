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
}


[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class CameraControllerSystem : SystemBase
{
    float3 camDirection;
    CollisionFilter filter;

    protected override void OnCreate()
    {
        RequireForUpdate<CameraAnchor>();
        RequireForUpdate<LocalPlayer>();
        filter = CollisionFilter.Default;
        filter.CollidesWith = ~(1u << 3);
        filter.BelongsTo = ~0u;
    }

    protected override void OnUpdate()
    {
        var anchorLtw = SystemAPI.GetComponent<LocalToWorld>(SystemAPI.GetSingletonEntity<CameraAnchor>());
        var anchor = SystemAPI.GetSingleton<CameraAnchor>();

        var camDistance = anchor.ScrollFactor;
        var rotatedOffset = math.mul(anchorLtw.Rotation, anchor.MaxOffset);
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

        var camTransform = Camera.main.transform;
        camTransform.position = anchorLtw.Position + rotatedOffset * camDistance;
        camTransform.rotation = anchorLtw.Rotation;
        camTransform.RotateAround(anchorLtw.Position, anchorLtw.Right, anchor.XRotation );
    }
}
