using System.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

[GenerateAuthoringComponent]
public struct GhostPlacementBuilder : IComponentData
{
    public Grid TargetGrid;
    public float Range;
    public bool Buildable;
    public GridItem Original;
}

public struct Ghost : IComponentData { }

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
        RequireSingletonForUpdate<SelectedItem>();
        RequireForUpdate(GetEntityQuery(typeof(BuildableBuffer), typeof(SelectedItem)));
    }

    protected override void OnStartRunning()
    {
        currentGrid = GetSingleton<BuilderMode_Build>().Grid;
        buildables = GetSingletonEntity<SelectedItem>();

        if (uiRoot == null)
            uiRoot = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("BuildMenu");
        uiRoot.Clear();

        var buffer = GetBuffer<BuildableBuffer>(buildables);
        var prefabs = BuildingPartManagementSystem.BuildingPartById;
        foreach (var id in buffer)
        {
            var button = new BuilderButton {Prefab = prefabs[id.PrefabId]};
            button.Q<Label>().text = GetComponent<Name>(prefabs[id.PrefabId]).Value.Value;
            uiRoot.Add(button);
        }
        uiRoot.style.display = DisplayStyle.Flex;
    }

    protected override void OnStopRunning()
    {
        uiRoot.style.display = DisplayStyle.None;
        if(HasSingleton<GhostPlacementBuilder>())
            EntityManager.DestroyEntity(GetSingletonEntity<GhostPlacementBuilder>());
    }

    protected override void OnUpdate()
    {
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
        if (bgp.Buildable && Input.GetMouseButtonDown(0))
        {
            var position = GetComponent<GridItem>(ghostEntity).Area.Origin;
            var rotation = GetComponent<Rotation>(ghostEntity);
            var buffer = GetBuffer<BuildableBuffer>(buildables);
            var pendingBuild = new PendingPart
            {
                Data = new BuildingPartInfo
                {
                    Id = buffer[ghostId].PrefabId,
                    Position = position,
                    Orientation = rotation.Value
                },
                Grid = currentGrid
            };
            var e = EntityManager.CreateEntity(typeof(PendingPart));
            EntityManager.SetComponentData(e, pendingBuild);
            EntityManager.DestroyEntity(ghostEntity);

            var item = GetSingletonEntity<SelectedItem>();
            if (HasComponent<Stackable>(item))
            {
                var stack = GetComponent<Stackable>(item);
                if(stack.Size > 0)
                    stack.Size--;
                if(stack.Size == 0)
                    EntityManager.DestroyEntity(item);
                else
                    SetComponent(item, stack);
            } else
                EntityManager.DestroyEntity(item);
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
        else if (input.ScrollMode == InputSystem.ScrollMode.Context)
        {
            ghostId += (int)math.sign(input.Scroll);;
            ghostId = ghostId.mod(GetBuffer<BuildableBuffer>(buildables).Length);
            EntityManager.DestroyEntity(ghostEntity);
        }
    }
}
