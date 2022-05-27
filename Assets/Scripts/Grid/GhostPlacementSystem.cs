using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

[GenerateAuthoringComponent]
public struct GhostPlacementBuilder : IComponentData
{
    public Entity TargetGrid;
    public float Range;
    public GridItem Original;
    public BoundingBox Area;
}

public struct Ghost : IComponentData
{
    public bool Buildable;
}

public struct Blueprinted : IComponentData
{
}

[UpdateBefore(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(BuildingSystem))]
[UpdateAfter(typeof(PlayerTargetSystem))]
public partial class GhostPlacementSystem : SystemBase
{
    private int ghostId;
    private bool rotating;
    private bool wantSwitchGhost;
    public quaternion currentRotation;
    private VisualElement buildMenu, constructionBorder;
    private Entity currentGrid;
    private Entity buildables;

    protected override void OnCreate()
    {
        currentRotation = quaternion.identity;

        RequireSingletonForUpdate<BuilderMode_Blueprint>();
        RequireSingletonForUpdate<LocalPlayer>();
        RequireForUpdate(GetEntityQuery(typeof(LocalPlayer), typeof(Docked)));
        RequireSingletonForUpdate<IdentifiableMappingSystem.Loaded>();
    }

    protected override void OnStartRunning()
    {
        currentGrid = GetSingleton<BuilderMode_Blueprint>().Grid;
        ghostId = 0;

        buildMenu ??= GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("BuildMenu");
        buildMenu.style.display = DisplayStyle.None;
        constructionBorder ??= GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("BlueprintBorder");
        constructionBorder.style.display = DisplayStyle.Flex;
        buildMenu.Clear();

        buildables = EntityManager.CreateEntity(typeof(BuildableBuffer));
        var buffer = GetBuffer<BuildableBuffer>(buildables);

        var query = GetEntityQuery(typeof(Prefab), typeof(GridItem), typeof(Identifieable), typeof(Name));
        var names = query.ToComponentDataArray<Name>(Allocator.Temp);
        var ids = query.ToComponentDataArray<Identifieable>(Allocator.Temp);
        for (int i = 0; i < ids.Length; i++)
        {
            buffer.Add(new BuildableBuffer {PrefabId = ids[i].ItemId});
            var button = new BuilderButton {Id = ids[i].ItemId};
            button.Q<Label>().text = names[i].Value.Value;
            var tempId = i;
            button.RegisterCallback<MouseEnterEvent>(_ =>
            {
                ghostId = tempId;
                wantSwitchGhost = true;
            });
            buildMenu.Add(button);
        }
    }

    protected override void OnStopRunning()
    {
        constructionBorder.style.display = DisplayStyle.None;
        buildMenu.style.display = DisplayStyle.None;
        if(HasSingleton<InputSystem.ControlHotMenu>())
            World.GetExistingSystem<InputSystem>().SetCursor(InputSystem.CursorState.Disabled);

        // DONT destroy with query as parameter, it will throw errors when not all children are marked as ghost as well
        foreach(var entity in GetEntityQuery(typeof(Ghost)).ToEntityArray(Allocator.Temp))
            if(EntityManager.Exists(entity))
                EntityManager.DestroyEntity(entity);
    }

    protected override void OnUpdate()
    {

        if (Input.GetKeyDown(KeyCode.T))
        {
            buildMenu.style.display = DisplayStyle.Flex;
            World.GetExistingSystem<InputSystem>().SetCursor(InputSystem.CursorState.HotMenu);
        }
        else if (Input.GetKeyUp(KeyCode.T))
        {
            buildMenu.style.display = DisplayStyle.None;
            World.GetExistingSystem<InputSystem>().SetCursor(InputSystem.CursorState.Disabled);
        }

        var query = GetEntityQuery(typeof(GhostPlacementBuilder));
        var placers = query.CalculateEntityCount();
        if (wantSwitchGhost || placers > 1)
        {
            EntityManager.DestroyEntity(query.ToEntityArray(Allocator.Temp));
            wantSwitchGhost = false;
            return;
        }

        if (!HasSingleton<GhostPlacementBuilder>())
        {
            var buffer = GetBuffer<BuildableBuffer>(buildables);
            var prefabId = buffer[ghostId].PrefabId;
            buildMenu.Query<BuilderButton>().ForEach(x => x.Selected = x.Id == prefabId);
            var pp = EntityManager.CreateEntity(typeof(PendingPart), typeof(Ghost));
            SetComponent(pp, new PendingPart
            {
                Data = new BuildingPartInfo
                {
                    Id = prefabId,
                    Position = int3.zero,
                    Orientation = new RightAngleRotation(currentRotation),
                    BuildStatus = 0
                },
                Grid = currentGrid
            });
            var ppquery = GetEntityQuery(typeof(Ghost), typeof(PendingPart));
            new BuildingSystem.BuildJob
            {
                buffer = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>().CreateCommandBuffer(),
                grids = GetComponentDataFromEntity<Grid>(true),
                gridItems = GetComponentDataFromEntity<GridItem>(true),
                occupations = GetBufferFromEntity<OccupationBuffer>(true),
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
                .ForEach((Entity e, in GridItem item, in Rotation r, in Identifieable id, in Ghost ghost) =>
                {
                    if (!ghost.Buildable)
                        return;

                    if (bgp.Original.Scalable)
                    {
                        var pendingBuild = new PendingPart
                        {
                            Data = new BuildingPartInfo
                            {
                                Id = id.ItemId,
                                Position = bgp.Area.Min,
                                Orientation = new RightAngleRotation(r.Value),
                                BuildStatus = 0,
                                Scale = bgp.Area.Max - bgp.Area.Min
                            },
                            Grid = currentGrid
                        };
                        var part = EntityManager.CreateEntity(typeof(PendingPart));
                        EntityManager.SetComponentData(part, pendingBuild);
                    }
                    else
                    {
                        for (int x = bgp.Area.Min.x; x <= bgp.Area.Max.x; x++)
                        for (int y = bgp.Area.Min.y; y <= bgp.Area.Max.y; y++)
                        for (int z = bgp.Area.Min.z; z <= bgp.Area.Max.z; z++)
                        {
                            var pendingBuild = new PendingPart
                            {
                                Data = new BuildingPartInfo
                                {
                                    Id = id.ItemId,
                                    Position = new int3(x,y,z),
                                    Orientation = new RightAngleRotation(r.Value),
                                    BuildStatus = 0
                                },
                                Grid = currentGrid
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

            SetComponent(placement, BuildingSystem.RotateItem(bgp.Original, currentRotation));
            EntityManager.SetComponentData(GetSingletonEntity<GhostPlacementBuilder>(), new Rotation{Value = currentRotation});
        }
    }
}
