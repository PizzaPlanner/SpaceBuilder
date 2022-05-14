using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;

[GenerateAuthoringComponent]
public struct Target : IComponentData
{
    public Entity Value;
    public Entity PreviousValue;
    public bool CanInteract;
    public bool CanConstruct;
}

public partial class TargetSystem : SystemBase
{
    BuildPhysicsWorld physicsWorld;
    private const float TARGET_MAX_RANGE = 4;
    private CollisionFilter filter;

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
        RequireSingletonForUpdate<CameraAnchor>();
        physicsWorld = World.GetExistingSystem<BuildPhysicsWorld>();
        filter = CollisionFilter.Default;
        filter.CollidesWith = (1u << 7) | (1u << 12);
        filter.BelongsTo = ~0u;
    }

    protected override void OnStartRunning()
    {
        physicsWorld.RegisterPhysicsRuntimeSystemReadOnly();
    }

    protected override void OnUpdate()
    {
        var camera = Camera.main.transform;
        var collisionWorld = physicsWorld.PhysicsWorld.CollisionWorld;
        var input = new RaycastInput
        {
            Start = camera.position,
            End = camera.position + camera.forward * TARGET_MAX_RANGE,
            Filter = filter
        };

        var target = GetComponent<Target>(GetSingletonEntity<LocalPlayer>());
        target.PreviousValue = target.Value;
        Debug.DrawLine(input.Start, input.End);
        if (collisionWorld.CastRay(input, out var hit) && HasComponent<Targetable>(hit.Entity))
            target.Value = hit.Entity;
        else
            target.Value = Entity.Null;
        SetComponent(GetSingletonEntity<LocalPlayer>(), target);

        Entities.ForEach((ref Target t) =>
        {
            var isGhost = t.Value != Entity.Null && HasComponent<Ghost>(t.Value);
            t.CanConstruct = !isGhost && t.Value != Entity.Null && HasComponent<UnderConstruction>(t.Value);
            t.CanInteract = !t.CanConstruct && !isGhost && t.Value != Entity.Null && HasComponent<Interactable>(t.Value);
        }).Run();
    }
}
