using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

public struct PlayerTargetModeSettings : IComponentData
{
    public PlayerTargetSystem.PlayerTargetMode Mode;
}

public partial struct PlayerTargetSystem : ISystem
{
    private const float TARGET_MAX_RANGE = 100;
    private const float TARGET_MAX_RANGE_CONSTRUCTABLE = 15;
    private const float TARGET_MAX_RANGE_INTERACTABLE = 7;
    private NativeHashMap<byte, CollisionFilter> collisionFilterMap;

    public enum PlayerTargetMode : byte
    {
        Interactions,
        Build,
        ScaleGhost,
    }
    
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate<LocalPlayer>();
        state.RequireForUpdate<CameraAnchor>();
        state.RequireForUpdate<PlayerTargetModeSettings>();

        state.EntityManager.CreateEntity(typeof(PlayerTargetModeSettings));
        SystemAPI.SetSingleton(new PlayerTargetModeSettings { Mode = PlayerTargetMode.Interactions });

        collisionFilterMap = new NativeHashMap<byte, CollisionFilter>(0, Allocator.Persistent);
        var fInteractions = CollisionFilter.Default;
        fInteractions.CollidesWith = (1u << 7);
        fInteractions.BelongsTo = ~0u;
        collisionFilterMap.Add((byte)PlayerTargetMode.Interactions, fInteractions);
        
        var fBuild = CollisionFilter.Default;
        fBuild.CollidesWith = (1 << 5);
        fBuild.BelongsTo = ~0u;
        collisionFilterMap.Add((byte)PlayerTargetMode.Build, fBuild);
        
        var fScaleGhost = fBuild;
        fScaleGhost.CollidesWith += (1 << 6);
        collisionFilterMap.Add((byte)PlayerTargetMode.ScaleGhost, fScaleGhost);
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
        {
            Debug.LogError($"No collision filter defined for target mode {targetMode}");
            return;
        }

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

        var localPlayer = SystemAPI.GetSingletonEntity<LocalPlayer>();
        var targetRW = SystemAPI.GetComponentRW<Target>(localPlayer);
        var target = targetRW.ValueRW;
        
        target.PreviousValue = target.Value;
        target.PreviousHit = target.Hit;
        target.Value = hit.Entity;
        if (target.Value != Entity.Null)
            target.Hit = hit.Position;
        
        target.CanInteract = target.Value != Entity.Null &&
                                SystemAPI.HasComponent<Interactable>(target.Value) &&
                                math.distancesq(hit.Position, camPosition) <= TARGET_MAX_RANGE_INTERACTABLE * TARGET_MAX_RANGE_INTERACTABLE;
            
        target.GridHit.Normal = 0;
        target.GridHit.Cell = int.MaxValue;
        target.GridHit.Grid = Entity.Null;

        var grid = target.Value;
        var parentLookup = SystemAPI.GetComponentLookup<Parent>(true);
        while (grid != Entity.Null && parentLookup.TryGetComponent(grid, out var parent))
            grid = parent.Value;
        
        if(SystemAPI.HasComponent<GridStructure>(grid))
        {
            // Calculate which grid cell was hit and on which side
            var gridLtw = SystemAPI.GetComponent<LocalToWorld>(grid);

            var cellHit = GridStructure.CellFromPosition(gridLtw, target.Hit + camForward * GridStructure.CELL_SIZE * 0.1f);
            var cellHitCenter = GridStructure.CellCenterToWorld(gridLtw, cellHit);
            var hitDirection = target.Hit - cellHitCenter;
            Debug.DrawLine(target.Hit, cellHitCenter, Color.red);
            var absDiffLocal = math.abs(hitDirection);
            bool3 biggest = new bool3(
                absDiffLocal.x > absDiffLocal.y && absDiffLocal.x >= absDiffLocal.z,
                absDiffLocal.y >= absDiffLocal.x && absDiffLocal.y > absDiffLocal.z,
                absDiffLocal.z >= absDiffLocal.y && absDiffLocal.z > absDiffLocal.x);
            var normalLocal = (int3)math.select(0, math.sign(hitDirection), biggest);
            
            target.GridHit.Cell = cellHit;
            target.GridHit.Normal = normalLocal;
            target.GridHit.Grid = grid;
        }

        targetRW.ValueRW = target;
    }
}