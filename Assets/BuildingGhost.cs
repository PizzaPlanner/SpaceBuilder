using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[GenerateAuthoringComponent]
public struct BuildingGhostPlacement : IComponentData
{
    public Grid TargetGrid;
    public int3 CurrentSlot;
    public float Range;
    public quaternion Rotation;
}

[UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]
public class BuildingPlacementActivationSystem : SystemBase
{
    private GhostBuilder builder;
    private BlobAssetStore blobStore;
    private GameObjectConversionSettings settings;
    private int ghostId;
    private bool rotating;

    protected override void OnCreate()
    {
        blobStore = new BlobAssetStore();
        settings = GameObjectConversionSettings.FromWorld(World, blobStore);
        builder = GameObject.FindObjectOfType<GhostBuilder>();
        builder.MakePrefabs(settings);

        RequireSingletonForUpdate<LocalPlayer>();
    }

    protected override void OnDestroy()
    {
        blobStore.Dispose();
    }

    protected override void OnUpdate()
    {
        var currentGrid = GetComponent<Parent>(GetSingletonEntity<LocalPlayer>()).Value;
        if (HasSingleton<BuildingGhostPlacement>())
        {
            if (!HasComponent<Grid>(currentGrid) || Input.GetKeyDown(KeyCode.B))
            {
                EntityManager.DestroyEntity(GetSingletonEntity<BuildingGhostPlacement>());
                return;
            }
            GhostBuilder.WireframeDisplay.ShowAxes(new bool3(Input.GetKey(KeyCode.Alpha1), Input.GetKey(KeyCode.Alpha2), Input.GetKey(KeyCode.Alpha3)));
            if (Input.GetAxis("Mouse ScrollWheel") != 0)
            {
                var sign = (int) math.sign(Input.GetAxis("Mouse ScrollWheel"));
                var bgp = GetSingleton<BuildingGhostPlacement>();
                if (Input.GetKey(KeyCode.Alpha1))
                {
                    bgp.Rotation = math.mul(bgp.Rotation, quaternion.RotateX(math.PI * sign * 0.5f));
                    SetSingleton(bgp);
                    rotating = true;
                }

                if (Input.GetKey(KeyCode.Alpha2))
                {
                    bgp.Rotation = math.mul(bgp.Rotation, quaternion.RotateY(math.PI * sign * 0.5f));
                    SetSingleton(bgp);
                    rotating = true;
                }

                if (Input.GetKey(KeyCode.Alpha3))
                {
                    bgp.Rotation = math.mul(bgp.Rotation, quaternion.RotateZ(math.PI * sign * 0.5f));
                    SetSingleton(bgp);
                    rotating = true;
                }

                if (!rotating)
                {
                    ghostId += sign;
                    ghostId = (ghostId + builder.EntityPrefabs.Length) % builder.EntityPrefabs.Length;
                    EntityManager.DestroyEntity(GetSingletonEntity<BuildingGhostPlacement>());
                    Build(currentGrid, true);
                    SetSingleton(bgp);
                }
            }
            else rotating = false;
        }
        else if(Input.GetKeyDown(KeyCode.B))
        {
            Build(currentGrid, true);
        }
    }

    public Entity Build(Entity gridEntity, bool asGhost)
    {
        if (!HasComponent<Grid>(gridEntity))
            return Entity.Null;

        var grid = GetComponent<Grid>(gridEntity);
        var entity = EntityManager.Instantiate(builder.EntityPrefabs[ghostId]);
        EntityManager.AddComponentData(entity, new Parent {Value = gridEntity});
        EntityManager.AddComponentData(entity, new Scale{Value = grid.CellSize});
        EntityManager.AddComponent<LocalToParent>(entity);
        if (asGhost)
        {
            EntityManager.AddComponentData(entity, new BuildingGhostPlacement{
                TargetGrid = grid,
                CurrentSlot = new int3(int.MaxValue, int.MaxValue, int.MaxValue),
                Range = 3,
                Rotation = quaternion.identity
            });

            // TODO: children are added later by transform systems. Need to do this properly!
            var children = GetBufferFromEntity<Child>(true);
            RemoveColliders(entity, children);
        }

        return entity;
    }

    void RemoveColliders(Entity root, BufferFromEntity<Child> childBuffer)
    {
        if (HasComponent<PhysicsCollider>(root))
            EntityManager.RemoveComponent<PhysicsCollider>(root);
        if (!childBuffer.HasComponent(root)) return;
        foreach(var child in childBuffer[root])
            RemoveColliders(child.Value, childBuffer);
    }
}

[UpdateBefore(typeof(TransformSystemGroup))]
public class CalculateBuildingSlotSystem : SystemBase
{
    BuildPhysicsWorld physicsWorld;

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<BuildingGhostPlacement>();
        RequireSingletonForUpdate<CameraAnchor>();
        physicsWorld = World.GetExistingSystem<BuildPhysicsWorld>();
    }

    protected override void OnStartRunning()
    {
        GhostBuilder.WireframeDisplay.gameObject.SetActive(true);
    }

    protected override void OnStopRunning()
    {
        if(GhostBuilder.WireframeDisplay)
            GhostBuilder.WireframeDisplay.gameObject.SetActive(false);
    }

    protected override void OnUpdate()
    {
        var ltw = GetComponent<LocalToWorld>(GetSingletonEntity<CameraAnchor>());
        var bgpe = GetSingletonEntity<BuildingGhostPlacement>();
        var gridEntity = GetComponent<Parent>(bgpe).Value;
        var gridLtw = GetComponent<LocalToWorld>(gridEntity);
        var bgp = EntityManager.GetComponentData<BuildingGhostPlacement>(bgpe);

        var collisionWorld = physicsWorld.PhysicsWorld.CollisionWorld;

        float3 target = ltw.Position + ltw.Forward * bgp.TargetGrid.CellSize * bgp.Range;
        Debug.DrawLine(ltw.Position, target);
        for (float f = bgp.TargetGrid.CellSize * bgp.Range; f > 0; f -= bgp.TargetGrid.CellSize * 0.25f)
        {
            var input = new RaycastInput
            {
                Start = ltw.Position + ltw.Forward * f,
                End = ltw.Position
            };
            if (collisionWorld.CastRay(input, out var hit))
            {
                target = hit.Position;
                break;
            }
        }


        bgp.CurrentSlot = bgp.TargetGrid.CellFromPosition(gridLtw, target);

        // Debug.DrawLine(bgp.TargetGrid.PositionFromCell(gridLtw, int3.zero), bgp.TargetGrid.PositionFromCell(gridLtw, new int3(1,0,0)), Color.red);
        // Debug.DrawLine(bgp.TargetGrid.PositionFromCell(gridLtw, int3.zero), bgp.TargetGrid.PositionFromCell(gridLtw, new int3(0,1,0)),Color.green);
        // Debug.DrawLine(bgp.TargetGrid.PositionFromCell(gridLtw, int3.zero), bgp.TargetGrid.PositionFromCell(gridLtw, new int3(0,0,1)), Color.blue);
        //
        // Debug.DrawLine(bgp.TargetGrid.PositionFromCell(gridLtw, bgp.CurrentSlot), bgp.TargetGrid.PositionFromCell(gridLtw, bgp.CurrentSlot + new int3(1,0,0)), Color.red);
        // Debug.DrawLine(bgp.TargetGrid.PositionFromCell(gridLtw, bgp.CurrentSlot), bgp.TargetGrid.PositionFromCell(gridLtw, bgp.CurrentSlot + new int3(0,1,0)),Color.green);
        // Debug.DrawLine(bgp.TargetGrid.PositionFromCell(gridLtw, bgp.CurrentSlot), bgp.TargetGrid.PositionFromCell(gridLtw, bgp.CurrentSlot + new int3(0,0,1)), Color.blue);

        var newPos = new Translation {Value = bgp.TargetGrid.PositionFromCell(new LocalToWorld(), bgp.CurrentSlot)};
        SetComponent(GetSingletonEntity<BuildingGhostPlacement>(), newPos);
        Transform wireframe = GhostBuilder.WireframeDisplay.transform;
        wireframe.position = bgp.TargetGrid.PositionFromCell(gridLtw, bgp.CurrentSlot);
        wireframe.rotation = gridLtw.Rotation;
        wireframe.localScale = new float3(1,1,1) * bgp.TargetGrid.CellSize;

        if (Input.GetMouseButtonDown(0))
        {
            EntityManager.DestroyEntity(GetSingletonEntity<BuildingGhostPlacement>());
            var e = World.GetExistingSystem<BuildingPlacementActivationSystem>().Build(gridEntity, false);
            if(e != Entity.Null)
                SetComponent(e, newPos);
        }
    }
}
