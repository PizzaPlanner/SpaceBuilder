using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

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

[UpdateBefore(typeof(TransformSystemGroup))]
public partial class GhostPlacementSystem : SystemBase
{
    private int ghostId;
    private bool rotating;
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
        constructionBorder ??= GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("ConstructionBorder");
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
                if(HasSingleton<GhostPlacementBuilder>())
                    EntityManager.DestroyEntity(GetSingletonEntity<GhostPlacementBuilder>());
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

        if (!HasSingleton<GhostPlacementBuilder>())
        {
            var buffer = GetBuffer<BuildableBuffer>(buildables);
            buildMenu.Query<BuilderButton>().ForEach(x => x.Selected = x.Id == buffer[ghostId].PrefabId);
            World.GetExistingSystem<BuildingSystem>().Build(buffer[ghostId].PrefabId, currentGrid, int3.zero, currentRotation, true);
            return;
        }

        var bgp = GetSingleton<GhostPlacementBuilder>();
        var ghostEntity = GetSingletonEntity<GhostPlacementBuilder>();
        if (Input.GetMouseButtonUp(0))
        {
            EntityManager.RemoveComponent<GhostPlacementBuilder>(ghostEntity);
            Entities
                .WithNone<GhostPlacementBuilder>()
                .WithAll<Ghost>()
                .ForEach((Entity ghost, in GridItem item, in Rotation r, in Identifieable id) =>
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
                            Orientation = r.Value
                        },
                        Grid = currentGrid
                    };
                    var e = EntityManager.CreateEntity(typeof(PendingPart));
                    EntityManager.SetComponentData(e, pendingBuild);
                }
                EntityManager.DestroyEntity(ghost);
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

            SetComponent(ghostEntity, BuildingSystem.RotateItem(bgp.Original, currentRotation));
            EntityManager.SetComponentData(GetSingletonEntity<GhostPlacementBuilder>(), new Rotation{Value = currentRotation});
        }
    }
}
