using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;


public partial struct PlayerTargetSystem : ISystem
{
    private const float TARGET_MAX_RANGE = 100;
    private const float TARGET_MAX_RANGE_CONSTRUCTABLE = 15;
    private const float TARGET_MAX_RANGE_INTERACTABLE = 7;
    private NativeHashMap<byte, CollisionFilter> collisionFilterMap;
    
    public struct PlayerTargetModeSettings : IComponentData
    {
        public PlayerTargetMode Mode;
    }

    public enum PlayerTargetMode : byte
    {
        Interactions,
        Build
    }
    
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate<LocalPlayer>();
        state.RequireForUpdate<CameraAnchor>();
        state.RequireForUpdate<PlayerTargetModeSettings>();

        state.EntityManager.CreateEntity(typeof(PlayerTargetModeSettings));
        SystemAPI.SetSingleton(new PlayerTargetModeSettings{Mode = PlayerTargetMode.Build});

        collisionFilterMap = new NativeHashMap<byte, CollisionFilter>(0, Allocator.Persistent);
        var fInteractions = CollisionFilter.Default;
        fInteractions.CollidesWith = (1u << 7);
        fInteractions.BelongsTo = ~0u;
        collisionFilterMap.Add((byte)PlayerTargetMode.Interactions, fInteractions);
        
        var fBuild = CollisionFilter.Default;
        fBuild.CollidesWith = ~0u;
        fBuild.BelongsTo = ~0u;
        collisionFilterMap.Add((byte)PlayerTargetMode.Build, fBuild);
    }

    public void OnDestroy(ref SystemState state)
    {
        collisionFilterMap.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var camera = SystemAPI.GetSingleton<CameraAnchor>();
        var camPosition = camera.CameraPosition;
        var camForward = camera.CameraForward;
        var targetMode = SystemAPI.GetSingleton<PlayerTargetModeSettings>().Mode;
        if (!collisionFilterMap.TryGetValue((byte)targetMode, out var filter))
            return;

        var hitLocation = float3.zero;
        var nextTarget = Entity.Null;

        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        var collisionWorld = physicsWorld.CollisionWorld;
        // Find target
        var input = new RaycastInput
        {
            Start = camPosition,
            End = camPosition + camForward * TARGET_MAX_RANGE,
            Filter = filter
        };
        collisionWorld.CastRay(input, out var hit);

        if (hit.Entity != Entity.Null)
        {
            nextTarget = hit.Entity;
            hitLocation = hit.Position;
            Debug.DrawLine(camPosition, hit.Position);
        }

        var localPlayer = SystemAPI.GetSingletonEntity<LocalPlayer>();
        var targetRW = SystemAPI.GetComponentRW<Target>(localPlayer);
        var target = targetRW.ValueRW;
        
        target.PreviousValue = target.Value;
        target.PreviousHit = target.Hit;
        target.Value = SystemAPI.HasComponent<Targetable>(nextTarget) ? nextTarget : Entity.Null;
        if (target.Value != Entity.Null)
            target.Hit = hitLocation;
        SystemAPI.SetComponent(localPlayer, target);
        
        target.CanInteract = target.Value != Entity.Null &&
                                SystemAPI.HasComponent<Interactable>(target.Value) &&
                                math.distancesq(hitLocation, camPosition) <= TARGET_MAX_RANGE_INTERACTABLE * TARGET_MAX_RANGE_INTERACTABLE;
            
        target.GridHit.Normal = 0;
        target.GridHit.Cell = int.MaxValue;
        target.GridHit.Grid = Entity.Null;

        if (target.Value != Entity.Null && SystemAPI.HasComponent<GridItem>(target.Value))
        {
            // Calculate which grid cell was hit and on which side
            var gridItem = SystemAPI.GetComponent<GridItem>(target.Value);
            var grid = SystemAPI.GetComponent<Parent>(target.Value).Value;
            var gridLtw = SystemAPI.GetComponent<LocalToWorld>(grid);

            var centerWorld = math.mul(gridLtw.Rotation, gridItem.Area.Center * GridStructure.CELL_SIZE) +
                              gridLtw.Position;
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

            if (GridTools.ViewIntersectsPlaneFromGridCell(new float3x2(camPosition, camForward), gridLtw,
                    planeOriginLocal, normalLocal, out var planeHit))
            {
                target.GridHit.Cell = GridStructure.CellFromPosition(gridLtw, planeHit + camForward * 0.01f);
                target.GridHit.Normal = normalLocal;
                target.GridHit.Grid = grid;
            }
        }

        targetRW.ValueRW = target;
    }
}