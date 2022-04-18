using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;
using Quaternion = System.Numerics.Quaternion;

[GenerateAuthoringComponent]
public struct GhostPlacementBuilder : IComponentData
{
    public Grid TargetGrid;
    public float Range;
    public bool Buildable;
    public GridItem Original;
}

[UpdateBefore(typeof(TransformSystemGroup))]
public class GhostPlacementSystem : SystemBase
{
    private GhostBuilder builder;
    private BlobAssetStore blobStore;
    private GameObjectConversionSettings settings;
    private int ghostId;
    private bool rotating;
    public quaternion currentRotation;
    private VisualElement uiRoot;
    private Entity currentGrid;

    protected override void OnCreate()
    {
        blobStore = new BlobAssetStore();
        settings = GameObjectConversionSettings.FromWorld(World, blobStore);
        currentRotation = quaternion.identity;

        RequireSingletonForUpdate<BuilderMode_Build>();
        RequireSingletonForUpdate<LocalPlayer>();
    }

    protected override void OnStartRunning()
    {
        if (builder == null)
        {
            builder = GameObject.FindObjectOfType<GhostBuilder>();
            builder.MakePrefabs(settings);
        }

        if (uiRoot == null)
            uiRoot = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("BuildMenu");

        uiRoot.style.display = DisplayStyle.Flex;
        currentGrid = GetComponent<Parent>(GetSingletonEntity<LocalPlayer>()).Value;
    }

    protected override void OnStopRunning()
    {
        uiRoot.style.display = DisplayStyle.None;
        if(HasSingleton<GhostPlacementBuilder>())
            EntityManager.DestroyEntity(GetSingletonEntity<GhostPlacementBuilder>());
    }

    protected override void OnDestroy()
    {
        blobStore.Dispose();
    }

    protected override void OnUpdate()
    {
        if (!HasSingleton<GhostPlacementBuilder>())
        {
            Build(currentGrid, new float3(float.PositiveInfinity), true);
            return;
        }

        GhostBuilder.WireframeDisplay.ShowAxes(new bool3(Input.GetKey(KeyCode.Alpha1), Input.GetKey(KeyCode.Alpha2), Input.GetKey(KeyCode.Alpha3)), currentRotation);
        if (Input.GetAxis("Mouse ScrollWheel") != 0)
        {
            var sign = (int) math.sign(Input.GetAxis("Mouse ScrollWheel"));
            var bgp = GetSingleton<GhostPlacementBuilder>();
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
                ghostId = ghostId.mod(builder.EntityPrefabs.Length);
                EntityManager.DestroyEntity(entity);
                Build(currentGrid, new float3(float.PositiveInfinity), true);
            }
            else
            {
                SetComponent(entity, RotateItem(bgp.Original, currentRotation));
            }

            EntityManager.SetComponentData(GetSingletonEntity<GhostPlacementBuilder>(), new Rotation{Value = currentRotation});
        }
        else rotating = false;
    }

    public Entity Build(Entity gridEntity, float3 position, bool asGhost)
    {
        if (!HasComponent<Grid>(gridEntity))
            return Entity.Null;

        var grid = GetComponent<Grid>(gridEntity);
        var entity = EntityManager.Instantiate(builder.EntityPrefabs[ghostId]);
        EntityManager.AddComponentData(entity, new Parent { Value = gridEntity});
        EntityManager.AddComponentData(entity, new Rotation{ Value = currentRotation});
        EntityManager.AddComponentData(entity, new Translation{ Value = position });
        EntityManager.AddComponent<LocalToParent>(entity);

        var originalGridItem = GetComponent<GridItem>(entity);
        var newGridItem = RotateItem(originalGridItem, currentRotation);
        newGridItem.Area.Origin = Grid.CellFromPosition(grid, new LocalToWorld(), position);
        EntityManager.SetComponentData(entity, newGridItem);

        if (asGhost)
        {
            EntityManager.AddComponentData(entity, new GhostPlacementBuilder{
                TargetGrid = grid,
                Range = 3,
                Original = originalGridItem
            });

            uiRoot.Q("Current") .Q<Icon3D>().SetObject(builder.Prefabs[ghostId].gameObject);
            uiRoot.Q("Next")    .Q<Icon3D>().SetObject(builder.Prefabs[(ghostId + 1).mod(builder.Prefabs.Length)].gameObject);
            uiRoot.Q("Previous").Q<Icon3D>().SetObject(builder.Prefabs[(ghostId - 1).mod(builder.Prefabs.Length)].gameObject);

            uiRoot.Q("Current") .Q<Label>().text = builder.Prefabs[ghostId].gameObject.name;
            uiRoot.Q("Next")    .Q<Label>().text = builder.Prefabs[(ghostId + 1).mod(builder.Prefabs.Length)].gameObject.name;
            uiRoot.Q("Previous").Q<Label>().text = builder.Prefabs[(ghostId - 1).mod(builder.Prefabs.Length)].gameObject.name;
            #if UNITY_EDITOR
            EntityManager.SetName(entity, "[GHOST]Placement");
            #endif
        }
        return entity;
    }

    public static GridItem RotateItem(GridItem original, quaternion rotation)
    {
        var oc1 = original.Occupied;
        original.Airtight = original.Airtight.Rotate(rotation);
        original.Occupied = original.Occupied.Rotate(rotation);
        original.Area.RotatedExtents = (int3)math.round(math.mul(rotation, original.Area.Extents));
        return original;
    }
}

public struct GhostRenderer : IComponentData { }

[UpdateAfter(typeof(TransformSystemGroup))]
public class GhostPlacementUpdateChildrenSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;
    private GhostPlacementSettings settings;
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<GhostPlacementBuilder>();
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        settings = Resources.Load<GhostPlacementSettings>("GhostPlacementSettings");
    }

    protected override void OnUpdate()
    {
        var bfe = GetBufferFromEntity<Child>(true);
        var buffer = bufferSystem.CreateCommandBuffer();
        var buildable = GetSingleton<GhostPlacementBuilder>().Buildable;
        foreach (var child in bfe.GetAllChildren(GetSingletonEntity<GhostPlacementBuilder>()))
        {
            if (EntityManager.HasComponent<RenderMesh>(child))
            {
                var rm = EntityManager.GetSharedComponentData<RenderMesh>(child);
                rm.material = buildable ? settings.GhostPlacementMaterial : settings.GhostRemovalMaterial;
                buffer.SetSharedComponent(child, rm);
            }

            if (EntityManager.HasComponent<PhysicsCollider>(child))
            {
                buffer.RemoveComponent<PhysicsCollider>(child);
            }
        }
    }
}
