using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

[GenerateAuthoringComponent]
public struct GhostPlacementBuilder : IComponentData
{
    public Grid TargetGrid;
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
    private VisualElement uiRoot;
    private Entity currentGrid;
    private Entity buildables;

    protected override void OnCreate()
    {
        currentRotation = quaternion.identity;

        RequireSingletonForUpdate<BuilderMode_Build>();
        RequireSingletonForUpdate<LocalPlayer>();
        RequireForUpdate(GetEntityQuery(typeof(LocalPlayer), typeof(Docked)));
        RequireSingletonForUpdate<SelectedItem>();
        RequireForUpdate(GetEntityQuery(typeof(BuildableBuffer), typeof(SelectedItem)));
    }

    protected override void OnStartRunning()
    {
        currentGrid = GetSingleton<BuilderMode_Build>().Grid;
        buildables = GetSingletonEntity<SelectedItem>();
        ghostId = 0;

        GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("ConstructionBorder").style.display = DisplayStyle.Flex;
        uiRoot ??= GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("BuildMenu");
        uiRoot.Clear();

        var buffer = GetBuffer<BuildableBuffer>(buildables);
        var prefabs = BuildingPartManagementSystem.BuildingPartById;
        foreach (var id in buffer)
        {
            var button = new BuilderButton {Prefab = prefabs[id.PrefabId]};
            button.Q<Label>().text = GetComponent<Name>(prefabs[id.PrefabId]).Value.Value;
            uiRoot.Add(button);
        }
    }

    protected override void OnStopRunning()
    {
        GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("ConstructionBorder").style.display = DisplayStyle.None;
        uiRoot.style.display = DisplayStyle.None;

        // DONT destroy with query as parameter, it will throw errors when not all children are marked as ghost as well
        foreach(var entity in GetEntityQuery(typeof(Ghost)).ToEntityArray(Allocator.Temp))
            if(EntityManager.Exists(entity))
                EntityManager.DestroyEntity(entity);
    }

    protected override void OnUpdate()
    {
        uiRoot.style.display = Input.GetKey(KeyCode.T) ? DisplayStyle.Flex : DisplayStyle.None;
        if (!HasSingleton<GhostPlacementBuilder>())
        {
            var prefabs = BuildingPartManagementSystem.BuildingPartById;
            var buffer = GetBuffer<BuildableBuffer>(buildables);
            uiRoot.Query<BuilderButton>().ForEach(x => x.Selected = x.Prefab == prefabs[buffer[ghostId].PrefabId]);
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
                EntityManager.AddComponentData(buildables, new RemoveItem { Quantity = 1 });
                EntityManager.DestroyEntity(ghost);
            }).WithStructuralChanges().WithoutBurst().Run();
        }

        BuildingPartManagementSystem.WireframeDisplay.ShowAxes(new bool3(Input.GetKey(KeyCode.Alpha1), Input.GetKey(KeyCode.Alpha2), Input.GetKey(KeyCode.Alpha3)), currentRotation);
        var input = GetSingleton<InputSystem.InputData>();
        if (input.ScrollMode == InputSystem.ScrollMode.Rotate)
        {
            var sign = (int)math.sign(input.Scroll);
            if (Input.GetKey(KeyCode.Alpha1))
            {
                currentRotation = math.mul(currentRotation, quaternion.RotateX(math.PI * sign * 0.5f));
            }

            if (Input.GetKey(KeyCode.Alpha2))
            {
                currentRotation = math.mul(currentRotation, quaternion.RotateY(math.PI * sign * 0.5f));
            }

            if (Input.GetKey(KeyCode.Alpha3))
            {
                currentRotation = math.mul(currentRotation, quaternion.RotateZ(math.PI * sign * 0.5f));
            }

            SetComponent(ghostEntity, BuildingSystem.RotateItem(bgp.Original, currentRotation));
            EntityManager.SetComponentData(GetSingletonEntity<GhostPlacementBuilder>(), new Rotation{Value = currentRotation});
        }
        else if (input.ScrollMode == InputSystem.ScrollMode.Context && uiRoot.style.display == DisplayStyle.Flex)
        {
            ghostId += (int)math.sign(input.Scroll);;
            ghostId = ghostId.mod(GetBuffer<BuildableBuffer>(buildables).Length);
            EntityManager.DestroyEntity(ghostEntity);
        }
    }
}
