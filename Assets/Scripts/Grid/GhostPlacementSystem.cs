using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

public struct GhostPlacementBuilder : IComponentData
{
    public Entity TargetGrid;
    public GridItem Original;
    public int ScaleUp;
    public int3 DragStartCell;
    public int3 DragEndCell;
    public int3 DragStartNormal;

    public BoundingBox DragArea => new BoundingBox { Origin = DragStartCell, Extents = DragEndCell - DragStartCell };
}

public struct Ghost : IComponentData
{
    public bool Buildable;
}

public struct Blueprinted : IComponentData
{
}

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class BuildingUISystem : SystemBase
{
    private VisualElement buildMenu, constructionBorder;
    private GhostPlacementSystem placementSystem;
    protected override void OnCreate()
    {
        RequireForUpdate<BuilderMode_Blueprint>();
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<IdentifiableMappingSystem.Loaded>();
        placementSystem = World.GetExistingSystemManaged<GhostPlacementSystem>();
    }

    protected override void OnUpdate()
    {
        if (placementSystem.currentPrefabId == 0)
            constructionBorder.Q<Label>().text = "<Press T to select a Part>";
        else if (HasSingleton<GhostPlacementBuilder>() && GetSingleton<GhostPlacementBuilder>().TargetGrid != Entity.Null)
        {
            constructionBorder.Q<Label>().text =
                GetComponent<Name>(GetSingleton<GhostPlacementBuilder>().TargetGrid).Value.Value;
        }
        else
            constructionBorder.Q<Label>().text = "<No Target>";

        if (Input.GetKeyDown(KeyCode.T))
        {
            buildMenu.style.display = DisplayStyle.Flex;
            World.GetExistingSystemManaged<InputSystem>().SetCursor(InputSystem.CursorState.HotMenu);
        }
        else if (Input.GetKeyUp(KeyCode.T))
        {
            buildMenu.style.display = DisplayStyle.None;
            World.GetExistingSystemManaged<InputSystem>().SetCursor(InputSystem.CursorState.Disabled);
        }
    }

    protected override void OnStartRunning()
    {
        buildMenu ??= GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("BuildMenu");
        buildMenu.style.display = DisplayStyle.None;
        constructionBorder ??= GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("BlueprintBorder");
        constructionBorder.style.display = DisplayStyle.Flex;
        buildMenu.Clear();

        var query = GetEntityQuery(typeof(Prefab), typeof(GridItem), typeof(Identifieable), typeof(Name));
        var names = query.ToComponentDataArray<Name>(Allocator.Temp);
        var ids = query.ToComponentDataArray<Identifieable>(Allocator.Temp);
        for (int i = 0; i < ids.Length; i++)
        {
            var button = new BuilderButton {Id = ids[i].ItemId};
            button.Q<Label>().text = names[i].Value.Value;
            button.RegisterCallback<MouseEnterEvent>(_ =>
            {
                SetSelectedPrefab(button.Id);
            });
            buildMenu.Add(button);
        }
    }

    protected override void OnStopRunning()
    {
        constructionBorder.style.display = DisplayStyle.None;
        buildMenu.style.display = DisplayStyle.None;
        if(HasSingleton<InputSystem.ControlHotMenu>())
            World.GetExistingSystemManaged<InputSystem>().SetCursor(InputSystem.CursorState.Disabled);
    }

    void SetSelectedPrefab(ulong prefabId)
    {
        buildMenu.Query<BuilderButton>().ForEach(x => x.Selected = x.Id == prefabId);
        placementSystem.currentPrefabId = prefabId;
        placementSystem.wantSwitchGhost = true;
    }
}

[UpdateBefore(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(BuildingSystem))]
[UpdateBefore(typeof(CalculateBuildingSlotSystem))]
[UpdateAfter(typeof(PlayerTargetSystem))]
public partial class GhostPlacementSystem : SystemBase
{
    public ulong currentPrefabId;
    public bool wantSwitchGhost;
    public quaternion currentRotation;
    private Entity currentGrid;

    protected override void OnCreate()
    {
        currentRotation = quaternion.identity;

        RequireForUpdate<BuilderMode_Blueprint>();
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<IdentifiableMappingSystem.Loaded>();
    }

    protected override void OnStopRunning()
    {
        // DONT destroy with query as parameter, it will throw errors when not all children are marked as ghost as well
        foreach(var entity in GetEntityQuery(typeof(Ghost)).ToEntityArray(Allocator.Temp))
            if(EntityManager.Exists(entity))
                EntityManager.DestroyEntity(entity);
    }

    protected override void OnUpdate()
    {
        var query = GetEntityQuery(typeof(GhostPlacementBuilder));
        var placers = query.CalculateEntityCount();
        if (wantSwitchGhost || placers > 1)
        {
            EntityManager.DestroyEntity(query.ToEntityArray(Allocator.Temp));
            wantSwitchGhost = false;
        }

        if (!HasSingleton<GhostPlacementBuilder>())
        {
            if (currentPrefabId == 0)
                return;

            var target = GetComponent<Target>(GetSingletonEntity<LocalPlayer>());
            if (target.GridHit.Grid == Entity.Null)
                return;

            var pp = EntityManager.CreateEntity(typeof(PendingPart), typeof(Ghost));
            SetComponent(pp, new PendingPart
            {
                Data = new BuildingPartInfo
                {
                    Id = currentPrefabId,
                    Orientation = new RightAngleRotation(currentRotation),
                    BuildStatus = 0
                },
                Grid = target.GridHit.Grid
            });
            var ppquery = GetEntityQuery(typeof(Ghost), typeof(PendingPart));
            new BuildingSystem.BuildJob
            {
                buffer = GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged),
                grids = GetComponentLookup<GridStructure>(true),
                gridItems = GetComponentLookup<GridItem>(true),
                colliders = GetComponentLookup<PhysicsCollider>(true),
                occupations = GetComponentLookup<CellMap>(true),
                prefabMap = IdentifiableMappingSystem.EntitiesById,
                IsCreateGhostJob = true
            }.Run(ppquery);
            EntityManager.DestroyEntity(pp);
            return;
        }

        var bgp = GetSingleton<GhostPlacementBuilder>();
        var placement = GetSingletonEntity<GhostPlacementBuilder>();
        if (Input.GetMouseButtonUp(0))
        {
            Entities
                .ForEach((Entity e, in GridItem item, in LocalTransform transform, in Identifieable id, in Ghost ghost) =>
                {
                    if (!ghost.Buildable || bgp.TargetGrid == Entity.Null)
                        return;

                    if (bgp.Original.Scalable)
                    {
                        var pendingBuild = new PendingPart
                        {
                            Data = new BuildingPartInfo
                            {
                                Id = id.ItemId,
                                Area = item.Area,
                                Orientation = new RightAngleRotation(transform.Rotation),
                                BuildStatus = 0,
                            },
                            Grid = bgp.TargetGrid
                        };
                        var part = EntityManager.CreateEntity(typeof(PendingPart));
                        EntityManager.SetComponentData(part, pendingBuild);
                    }
                    else
                    {
                        for (int x = bgp.DragArea.Min.x; x <= bgp.DragArea.Max.x; x++)
                        for (int y = bgp.DragArea.Min.y; y <= bgp.DragArea.Max.y; y++)
                        for (int z = bgp.DragArea.Min.z; z <= bgp.DragArea.Max.z; z++)
                        {
                            var pendingBuild = new PendingPart
                            {
                                Data = new BuildingPartInfo
                                {
                                    Id = id.ItemId,
                                    Area = new BoundingBox{Origin = new int3(x,y,z)},
                                    Orientation = new RightAngleRotation(transform.Rotation),
                                    BuildStatus = 0
                                },
                                Grid = bgp.TargetGrid
                            };
                            var part = EntityManager.CreateEntity(typeof(PendingPart));
                            EntityManager.SetComponentData(part, pendingBuild);
                        }
                    }
                    EntityManager.DestroyEntity(e);
            }).WithStructuralChanges().WithoutBurst().Run();
        }

        GhostBuilder.Wireframe.ShowAxes(new bool3(Input.GetKey(KeyCode.Alpha1), Input.GetKey(KeyCode.Alpha2), Input.GetKey(KeyCode.Alpha3)), currentRotation);
        var input = GetSingleton<InputSystem.InputData>();
        if (input.ScrollMode == InputSystem.ScrollMode.Rotate)
        {
            var sign = (int)math.sign(input.Scroll);
            if (Input.GetKey(KeyCode.Alpha1))
                currentRotation = math.mul(currentRotation, quaternion.RotateX(math.PI * sign * 0.5f));
            if (Input.GetKey(KeyCode.Alpha2))
                currentRotation = math.mul(currentRotation, quaternion.RotateY(math.PI * sign * 0.5f));
            if (Input.GetKey(KeyCode.Alpha3))
                currentRotation = math.mul(currentRotation, quaternion.RotateZ(math.PI * sign * 0.5f));

            SystemAPI.SetComponent(placement, bgp.Original);
            var builder = SystemAPI.GetSingletonEntity<GhostPlacementBuilder>();
            var transform = SystemAPI.GetComponent<LocalTransform>(builder);
            SystemAPI.SetComponent(builder, transform.WithRotation(currentRotation));
        }
    }
}
