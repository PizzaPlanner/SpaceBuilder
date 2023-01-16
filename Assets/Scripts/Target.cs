using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;


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
    private const float TARGET_MAX_RANGE = 100;
    private const float TARGET_MAX_RANGE_CONSTRUCTABLE = 15;
    private const float TARGET_MAX_RANGE_INTERACTABLE = 7;
    private CollisionFilter filterInteractable, filterConstructable;

    private PhysicsWorldData bpWorld;
    protected override void OnCreate()
    {
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<CameraAnchor>();
        filterInteractable = CollisionFilter.Default;
        filterInteractable.CollidesWith = (1u << 7);
        filterInteractable.BelongsTo = ~0u;
        filterConstructable = CollisionFilter.Default;
        filterConstructable.CollidesWith = ~(1u << 3);
        filterConstructable.BelongsTo = ~0u;
        
        bpWorld = new PhysicsWorldData(ref CheckedStateRef, new PhysicsWorldIndex(2));
    }

    protected override void OnDestroy()
    {
        bpWorld.Dispose();
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
            PhysicsWorldBuilder.BuildPhysicsWorldImmediate(ref CheckedStateRef, ref bpWorld, 0, float3.zero, LastSystemVersion);

            // Find Constructable Target
            var input2 = new RaycastInput
            {
                Start = camPosition,
                End = camPosition + camForward * TARGET_MAX_RANGE,
                Filter = filterConstructable
            };
            bpWorld.PhysicsWorld.CollisionWorld.CastRay(input2, out constructableHit);
        }
        
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        var collisionWorld = physicsWorld.CollisionWorld;
        // Find Other target
        var input = new RaycastInput
        {
            Start = camPosition,
            End = camPosition + camForward * TARGET_MAX_RANGE,
            Filter = filterInteractable
        };
        collisionWorld.CastRay(input, out var hit);

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

        var localPlayer = SystemAPI.GetSingletonEntity<LocalPlayer>();
        var target = SystemAPI.GetComponent<Target>(localPlayer);
        target.PreviousValue = target.Value;
        target.PreviousHit = target.Hit;
        target.Value = SystemAPI.HasComponent<Targetable>(nextTarget) ? nextTarget : Entity.Null;
        if (target.Value != Entity.Null)
            target.Hit = hitLocation;
        SystemAPI.SetComponent(localPlayer, target);

        Entities
            .WithAll<LocalPlayer>()
            .ForEach((ref Target t) =>
        {
            var isGhost = t.Value != Entity.Null && SystemAPI.HasComponent<Ghost>(t.Value);
            t.CanConstruct = !isGhost && t.Value != Entity.Null &&
                             SystemAPI.HasComponent<UnderConstruction>(t.Value) &&
                             math.distancesq(hitLocation, camPosition) <= TARGET_MAX_RANGE_CONSTRUCTABLE * TARGET_MAX_RANGE_CONSTRUCTABLE;
            t.CanInteract = !t.CanConstruct && !isGhost &&
                            t.Value != Entity.Null &&
                            SystemAPI.HasComponent<Interactable>(t.Value) &&
                            !SystemAPI.HasComponent<Blueprinted>(t.Value) &&
                            math.distancesq(hitLocation, camPosition) <= TARGET_MAX_RANGE_INTERACTABLE * TARGET_MAX_RANGE_INTERACTABLE;
            
            t.GridHit.Normal = 0;
            t.GridHit.Cell = int.MaxValue;
            t.GridHit.Grid = Entity.Null;
            if (t.Value == Entity.Null || !SystemAPI.HasComponent<GridItem>(target.Value)) return;
            // Calculate which grid cell was hit and on which side
            var gridItem = SystemAPI.GetComponent<GridItem>(target.Value);
            var gridLtw = SystemAPI.GetComponent<WorldTransform>(gridItem.Grid);

            var centerWorld = math.mul(gridLtw.Rotation, gridItem.Area.Center * GridStructure.CELL_SIZE) + gridLtw.Position;
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
            
            if (GridTools.ViewIntersectsPlaneFromGridCell(new float3x2(camPosition, camForward), gridLtw, planeOriginLocal, normalLocal, out var planeHit))
            {
                t.GridHit.Cell = GridStructure.CellFromPosition(gridLtw, planeHit + camForward * 0.01f);
                t.GridHit.Normal = normalLocal;
                t.GridHit.Grid = gridItem.Grid;
            }
        }).Run();
    }
}