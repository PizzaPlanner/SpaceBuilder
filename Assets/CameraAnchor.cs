using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

[GenerateAuthoringComponent]
public struct CameraAnchor : IComponentData
{
    public float ScrollFactor;
    public float3 Offset;
    public float XRotation;
}


[UpdateInGroup(typeof(PresentationSystemGroup))]
public class CameraControllerSystem : SystemBase
{
    float3 camDirection;

    BuildPhysicsWorld physicsWorld;
    CollisionFilter filter;

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<CameraAnchor>();
        RequireSingletonForUpdate<LocalPlayer>();
        physicsWorld = World.GetExistingSystem<BuildPhysicsWorld>();
        filter = CollisionFilter.Default;
        filter.CollidesWith = ~(1u << 3);
        filter.BelongsTo = ~0u;
    }

    protected override void OnUpdate()
    {
        var anchorLtw = GetComponent<LocalToWorld>(GetSingletonEntity<CameraAnchor>());
        var anchor = GetSingleton<CameraAnchor>();

        if (!HasSingleton<GhostPlacementBuilder>() && Input.GetAxis("Mouse ScrollWheel") != 0)
        {
            anchor.ScrollFactor -= Input.GetAxis("Mouse ScrollWheel");
            anchor.ScrollFactor = math.clamp(anchor.ScrollFactor, 0, 2);
            SetSingleton(anchor);
        }

        var camDistance = anchor.ScrollFactor;
        var rotatedOffset = math.mul(anchorLtw.Rotation, anchor.Offset);
        RaycastInput raycastInput = new RaycastInput
        {
            Start = anchorLtw.Position,
            End = anchorLtw.Position + rotatedOffset,
            Filter = filter
        };
        if (physicsWorld.PhysicsWorld.CollisionWorld.CastRay(raycastInput, out var raycastHit))
        {
            camDistance *= raycastHit.Fraction - 0.1f;
        }
        //
        // translation.Value = anchor.Offset * camDistance;

        Camera.main.transform.position = anchorLtw.Position + rotatedOffset * camDistance;
        Camera.main.transform.rotation = anchorLtw.Rotation;
        Camera.main.transform.RotateAround(anchorLtw.Position, anchorLtw.Right, anchor.XRotation );
    }
}
