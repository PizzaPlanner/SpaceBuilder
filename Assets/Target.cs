using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

[GenerateAuthoringComponent]
public struct Target : IComponentData
{
    public Entity Value;
    public Entity PreviousValue;
    public bool CanInteract;
    public bool CanConstruct;
}

public partial class PlayerTargetSystem : SystemBase
{
    public struct PhysicsWorld2Singleton : IComponentData { }

    BuildPhysicsWorld physicsWorld;
    private const float TARGET_MAX_RANGE = 4;
    private CollisionFilter filterInteractable, filterConstructable;

    private PhysicsWorldData bpWorld;
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
        RequireSingletonForUpdate<CameraAnchor>();
        physicsWorld = World.GetExistingSystem<BuildPhysicsWorld>();
        bpWorld = new PhysicsWorldData(EntityManager, new PhysicsWorldIndex(2));
        filterInteractable = CollisionFilter.Default;
        filterInteractable.CollidesWith = (1u << 7);
        filterInteractable.BelongsTo = ~0u;
        filterConstructable = CollisionFilter.Default;
        filterConstructable.CollidesWith = ~(1u << 3);
        filterConstructable.BelongsTo = ~0u;

        EntityManager.CreateEntity(typeof(PhysicsWorld2Singleton));
    }

    protected override void OnDestroy()
    {
        bpWorld.Dispose();
    }

    protected override void OnStartRunning()
    {
        physicsWorld.RegisterPhysicsRuntimeSystemReadOnly();
    }

    protected override void OnUpdate()
    {
        var camera = Camera.main.transform;

        var nextTarget = Entity.Null;

        PhysicsWorldBuilder.BuildPhysicsWorldImmediate(this, ref bpWorld, 0, float3.zero, LastSystemVersion);
        var input2 = new RaycastInput
        {
            Start = camera.position,
            End = camera.position + camera.forward * TARGET_MAX_RANGE,
            Filter = filterConstructable
        };
        if (bpWorld.PhysicsWorld.CollisionWorld.CastRay(input2, out var constructableHit) &&
            (!physicsWorld.PhysicsWorld.CollisionWorld.CastRay(input2, out var actualHit) || actualHit.Fraction > constructableHit.Fraction))
            nextTarget = constructableHit.Entity;


        if (nextTarget == Entity.Null)
        {
            var input = new RaycastInput
            {
                Start = camera.position,
                End = camera.position + camera.forward * TARGET_MAX_RANGE,
                Filter = filterInteractable
            };
            if (physicsWorld.PhysicsWorld.CollisionWorld.CastRay(input, out var hit))
                nextTarget = hit.Entity;
        }

        var target = GetComponent<Target>(GetSingletonEntity<LocalPlayer>());
        target.PreviousValue = target.Value;
        target.Value = HasComponent<Targetable>(nextTarget) ? nextTarget : Entity.Null;
        SetComponent(GetSingletonEntity<LocalPlayer>(), target);

        Entities.ForEach((ref Target t) =>
        {
            var isGhost = t.Value != Entity.Null && HasComponent<Ghost>(t.Value);
            t.CanConstruct = !isGhost && t.Value != Entity.Null && HasComponent<UnderConstruction>(t.Value);
            t.CanInteract = !t.CanConstruct && !isGhost && t.Value != Entity.Null && HasComponent<Interactable>(t.Value);
        }).Run();
    }
}

public partial class RepairStationTargetSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var gridQuery = GetEntityQuery(typeof(GridItem), typeof(UnderConstruction));
        var entities = gridQuery.ToEntityArray(Allocator.TempJob);
        var grids = gridQuery.ToComponentDataArray<GridItem>(Allocator.TempJob);
        Entities
            .WithNone<UnderConstruction>()
            .ForEach((ref Target target, in Constructor c, in GridItem item) =>
        {
            target.PreviousValue = target.Value;
            target.Value = Entity.Null;
            for(int i = 0; i < entities.Length && target.Value == Entity.Null; i++)
                if (grids[i].Grid == item.Grid)
                {
                    target.Value = entities[i];
                    target.CanConstruct = true;
                }
        }).Schedule();
    }
}
