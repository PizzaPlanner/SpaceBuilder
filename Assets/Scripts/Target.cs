using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

[GenerateAuthoringComponent]
public struct Target : IComponentData
{
    public Entity Value;
    public Entity PreviousValue;
    public float3 Hit;
    public float3 PreviousHit;
    public GridHitInfo GridHit;
    public bool CanInteract;
    public bool CanConstruct;
}

public struct GridHitInfo
{
    public int3 Normal;
    public int3 Cell;
    public Entity Grid;
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
        Dependency.Complete();

        var camera = Camera.main.transform;
        var camPosition = (float3)camera.position;
        var camForward = (float3) camera.forward;

        var hitLocation = float3.zero;
        var nextTarget = Entity.Null;

        RaycastHit constructableHit = default;
        if (GetEntityQuery(new EntityQueryDesc { Any = new[]
            {
                ComponentType.ReadOnly<BuilderMode_Blueprint>(), 
                ComponentType.ReadOnly<BuilderMode_Build>(), 
                ComponentType.ReadOnly<BuilderMode_Destroy>()
            } }).CalculateChunkCount() > 0)
        {
            PhysicsWorldBuilder.BuildPhysicsWorldImmediate(this, ref bpWorld, 0, float3.zero, LastSystemVersion);

            // Find Constructable Target
            var input2 = new RaycastInput
            {
                Start = camPosition,
                End = camPosition + camForward * TARGET_MAX_RANGE,
                Filter = filterConstructable
            };
            bpWorld.PhysicsWorld.CollisionWorld.CastRay(input2, out constructableHit);
        }

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
                             HasComponent<UnderConstruction>(t.Value) &&
                             math.distancesq(hitLocation, camPosition) <= TARGET_MAX_RANGE_CONSTRUCTABLE * TARGET_MAX_RANGE_CONSTRUCTABLE;
            t.CanInteract = !t.CanConstruct && !isGhost &&
                            t.Value != Entity.Null &&
                            HasComponent<Interactable>(t.Value) &&
                            !HasComponent<Blueprinted>(t.Value) &&
                            math.distancesq(hitLocation, camPosition) <= TARGET_MAX_RANGE_INTERACTABLE * TARGET_MAX_RANGE_INTERACTABLE;
            
            t.GridHit.Normal = 0;
            t.GridHit.Cell = int.MaxValue;
            t.GridHit.Grid = Entity.Null;
            if (t.Value == Entity.Null || !HasComponent<GridItem>(target.Value)) return;
            // Calculate which grid cell was hit and on which side
            var gridItem = GetComponent<GridItem>(target.Value);
            var gridLtw = GetComponent<LocalToWorld>(gridItem.Grid);

            var centerWorld = math.mul(gridLtw.Rotation, gridItem.Area.Center * Grid.CELL_SIZE) + gridLtw.Position;
            var diffLocal = math.mul(math.inverse(gridLtw.Rotation), target.Hit - centerWorld);
            var absDiffLocal = math.abs(diffLocal) / gridItem.Area.Size;
            bool3 biggest = new bool3(
                absDiffLocal.x >= absDiffLocal.y && absDiffLocal.x >= absDiffLocal.z,
                absDiffLocal.y >= absDiffLocal.x && absDiffLocal.y >= absDiffLocal.z,
                absDiffLocal.z >= absDiffLocal.y && absDiffLocal.z >= absDiffLocal.x
            );
            var normalLocal = (int3)math.select(0, math.sign(diffLocal), biggest);
            var planeOriginLocal = math.select(0, gridItem.Area.Max + 1, normalLocal == 1) +
                                   math.select(0, gridItem.Area.Min, normalLocal == -1) +
                                   math.select(0, gridItem.Area.Center, normalLocal == 0);
            
            // var planeOriginWorld = Grid.GridToWorld(gridLtw, planeOriginLocal);
            // var normalWorld = math.mul(gridLtw.Rotation, normalLocal);
            //
            // if (GridTools.RayIntersectsPlane(camForward, camPosition, planeOriginWorld, normalWorld, 100, out var planeHit))
            // {
            //     t.GridHit.Cell = Grid.CellFromPosition(gridLtw, planeHit + camForward * 0.01f);
            //     t.GridHit.Normal = normalLocal;
            //     t.GridHit.Grid = gridItem.Grid;
            // }
            if (GridTools.ViewIntersectsPlaneFromGridCell(new float3x2(camPosition, camForward), gridLtw, planeOriginLocal, normalLocal, out var planeHit))
            {
                t.GridHit.Cell = Grid.CellFromPosition(gridLtw, planeHit + camForward * 0.01f);
                t.GridHit.Normal = normalLocal;
                t.GridHit.Grid = gridItem.Grid;
            }
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
