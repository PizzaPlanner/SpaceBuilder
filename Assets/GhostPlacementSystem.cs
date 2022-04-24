using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

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
    // private VisualElement uiRoot;
    private Entity currentGrid;
    private Entity buildables;

    protected override void OnCreate()
    {
        currentRotation = quaternion.identity;

        RequireSingletonForUpdate<BuilderMode_Build>();
        RequireSingletonForUpdate<LocalPlayer>();
    }

    protected override void OnStartRunning()
    {
        // if (uiRoot == null)
        //     uiRoot = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("BuildMenu");
        //
        // uiRoot.style.display = DisplayStyle.Flex;
        currentGrid = GetSingleton<BuilderMode_Build>().Grid;
        if (buildables == Entity.Null)
        {
            buildables = EntityManager.CreateEntity(typeof(BuildableBuffer));
            var buildBuffer = EntityManager.GetBuffer<BuildableBuffer>(buildables);
            foreach (var prefab in BuildingPartManagementSystem.BuildingPartById.GetValueArray(Allocator.Temp))
                if(HasComponent<GridItem>(prefab) && HasComponent<Identifieable>(prefab))
                    buildBuffer.Add(new BuildableBuffer{Id = GetComponent<Identifieable>(prefab).ItemId});
        }
    }

    protected override void OnStopRunning()
    {
        // uiRoot.style.display = DisplayStyle.None;
        if(HasSingleton<GhostPlacementBuilder>())
            EntityManager.DestroyEntity(GetSingletonEntity<GhostPlacementBuilder>());
    }

    protected override void OnUpdate()
    {
        if (!HasSingleton<GhostPlacementBuilder>())
        {
            var buffer = GetBuffer<BuildableBuffer>(buildables);
            World.GetExistingSystem<BuildingSystem>().Build(buffer[ghostId].Id, currentGrid, int3.zero, currentRotation, true);

            // uiRoot.Q("Current") .Q<Icon3D>().SetObject(builder.Prefabs[ghostId].gameObject);
            // uiRoot.Q("Next")    .Q<Icon3D>().SetObject(builder.Prefabs[(ghostId + 1).mod(builder.Prefabs.Length)].gameObject);
            // uiRoot.Q("Previous").Q<Icon3D>().SetObject(builder.Prefabs[(ghostId - 1).mod(builder.Prefabs.Length)].gameObject);
            //
            // uiRoot.Q("Current") .Q<Label>().text = builder.Prefabs[ghostId].gameObject.name;
            // uiRoot.Q("Next")    .Q<Label>().text = builder.Prefabs[(ghostId + 1).mod(builder.Prefabs.Length)].gameObject.name;
            // uiRoot.Q("Previous").Q<Label>().text = builder.Prefabs[(ghostId - 1).mod(builder.Prefabs.Length)].gameObject.name;

            return;
        }

        var bgp = GetSingleton<GhostPlacementBuilder>();
        if (bgp.Buildable && Input.GetMouseButtonDown(0))
        {
            var ghostEntity = GetSingletonEntity<GhostPlacementBuilder>();
            var position = GetComponent<GridItem>(ghostEntity).Area.Origin;
            var rotation = GetComponent<Rotation>(ghostEntity);
            var buffer = GetBuffer<BuildableBuffer>(buildables);
            var pendingBuild = new PendingPart
            {
                Data = new BuildingPartInfo
                {
                    Id = buffer[ghostId].Id,
                    Position = position,
                    Orientation = rotation.Value
                },
                Grid = currentGrid
            };
            var e = EntityManager.CreateEntity(typeof(PendingPart));
            EntityManager.SetComponentData(e, pendingBuild);
            EntityManager.DestroyEntity(ghostEntity);
        }

        BuildingPartManagementSystem.WireframeDisplay.ShowAxes(new bool3(Input.GetKey(KeyCode.Alpha1), Input.GetKey(KeyCode.Alpha2), Input.GetKey(KeyCode.Alpha3)), currentRotation);
        if (Input.GetAxis("Mouse ScrollWheel") != 0)
        {
            var sign = (int) math.sign(Input.GetAxis("Mouse ScrollWheel"));
            if (Input.GetKey(KeyCode.Alpha1))
            {
                currentRotation = math.mul(currentRotation, quaternion.RotateX(math.PI * sign * 0.5f));
                rotating = true;
            }

            if (Input.GetKey(KeyCode.Alpha2))
            {
                currentRotation = math.mul(currentRotation, quaternion.RotateY(math.PI * sign * 0.5f));
                SetSingleton(bgp);
                rotating = true;
            }

            if (Input.GetKey(KeyCode.Alpha3))
            {
                currentRotation = math.mul(currentRotation, quaternion.RotateZ(math.PI * sign * 0.5f));
                rotating = true;
            }

            var entity = GetSingletonEntity<GhostPlacementBuilder>();
            if (!rotating)
            {
                ghostId += sign;
                ghostId = ghostId.mod(GetBuffer<BuildableBuffer>(buildables).Length);
                EntityManager.DestroyEntity(entity);
                return;
            }
            else
            {
                SetComponent(entity, BuildingSystem.RotateItem(bgp.Original, currentRotation));
            }

            EntityManager.SetComponentData(GetSingletonEntity<GhostPlacementBuilder>(), new Rotation{Value = currentRotation});
        }
        else rotating = false;
    }
}
