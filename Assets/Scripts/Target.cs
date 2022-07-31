using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using YamlDotNet.Serialization;
using RaycastHit = Unity.Physics.RaycastHit;

[GenerateAuthoringComponent]
public struct Target : IComponentData
{
    public Entity Value;
    public Entity PreviousValue;
    public float3 Hit;
    public float3 PreviousHit;
    public bool CanInteract;
    public bool CanConstruct;
}

public struct CellPathBuffer : IBufferElementData
{
    public int3 Value;
}

public partial class PlayerTargetSystem : SystemBase
{
    public struct PhysicsWorld2Singleton : IComponentData { }

    BuildPhysicsWorld physicsWorld;
    private const float TARGET_MAX_RANGE = 100;
    private const float TARGET_MAX_RANGE_CONSTRUCTABLE = 15;
    private const float TARGET_MAX_RANGE_INTERACTABLE = 7;
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
        EntityManager.CreateEntity(typeof(CellPathBuffer));
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
        var camPosition = (float3)camera.position;
        var camForward = (float3) camera.forward;

        var hitLocation = float3.zero;
        var nextTarget = Entity.Null;

        PhysicsWorldBuilder.BuildPhysicsWorldImmediate(this, ref bpWorld, 0, float3.zero, LastSystemVersion);

        // Find Constructable Target
        var input2 = new RaycastInput
        {
            Start = camPosition,
            End = camPosition + camForward * TARGET_MAX_RANGE_CONSTRUCTABLE,
            Filter = filterConstructable
        };
        bpWorld.PhysicsWorld.CollisionWorld.CastRay(input2, out var constructableHit);

        // Find Other target
        var input = new RaycastInput
        {
            Start = camPosition,
            End = camPosition + camForward * TARGET_MAX_RANGE,
            Filter = filterInteractable
        };
        physicsWorld.PhysicsWorld.CollisionWorld.CastRay(input, out var hit);

        if (constructableHit.Entity != Entity.Null &&
            (hit.Entity == Entity.Null || hit.Fraction > constructableHit.Fraction))
        {
            nextTarget = constructableHit.Entity;
            hitLocation = constructableHit.Position;
        }
        else if (hit.Entity != Entity.Null)
        {
            nextTarget = hit.Entity;
            hitLocation = hit.Position;
        }

        var target = GetComponent<Target>(GetSingletonEntity<LocalPlayer>());
        target.PreviousValue = target.Value;
        target.PreviousHit = target.Hit;
        target.Value = HasComponent<Targetable>(nextTarget) ? nextTarget : Entity.Null;
        if (target.Value != Entity.Null)
            target.Hit = hitLocation;
        SetComponent(GetSingletonEntity<LocalPlayer>(), target);

        Entities.ForEach((ref Target t) =>
        {
            var isGhost = t.Value != Entity.Null && HasComponent<Ghost>(t.Value);
            t.CanConstruct = !isGhost && t.Value != Entity.Null &&
                             HasComponent<UnderConstruction>(t.Value);
            t.CanInteract = !t.CanConstruct && !isGhost &&
                            t.Value != Entity.Null &&
                            HasComponent<Interactable>(t.Value) &&
                            math.distancesq(hitLocation, camPosition) <= TARGET_MAX_RANGE_INTERACTABLE * TARGET_MAX_RANGE_INTERACTABLE;
        }).Run();

        if (target.Value != target.PreviousValue || math.any(target.Hit != target.PreviousHit))
        {
            var cellBuffer = EntityManager.GetBuffer<CellPathBuffer>(GetSingletonEntity<CellPathBuffer>());
            cellBuffer.Clear();

            if (target.Value == Entity.Null || !HasComponent<GridItem>(target.Value))
                return;

            var grid = GetComponent<GridItem>(target.Value).Grid;
            var ltw = GetComponent<LocalToWorld>(grid);
            var start = Grid.CellFromPosition(ltw, Camera.main.transform.position);
            var end = Grid.CellFromPosition(ltw, target.Hit);

            var startRelative = (camPosition - Grid.PositionFromCell(ltw, start)) / Grid.CELL_SIZE;

            // DDA as described in https://lodev.org/cgtutor/raycasting.html
            var direction = math.normalizesafe(target.Hit - camPosition);
            var delta = math.select(math.abs(1 / direction), float.MaxValue, direction == 0);
            var step = (int3) math.sign(direction);
            var distToNextSide = math.select(startRelative, 1 - startRelative, step > 0) * delta;

            var next = start;
            while (math.any(next != end))
            {
                var smallFilter = math.select(int3.zero, 1, new bool3(
                    distToNextSide.x <= distToNextSide.y && distToNextSide.x <= distToNextSide.z,
                    distToNextSide.y <= distToNextSide.x && distToNextSide.y <= distToNextSide.z,
                    distToNextSide.z <= distToNextSide.y && distToNextSide.z <= distToNextSide.x));
                if (math.all(smallFilter == int3.zero))
                    break;
                distToNextSide += delta * smallFilter;
                next += step * smallFilter;
                cellBuffer.Add(new CellPathBuffer{Value = next});
            }
        }
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
            .WithDisposeOnCompletion(entities)
            .WithDisposeOnCompletion(grids)
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
