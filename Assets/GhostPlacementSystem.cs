using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;
using Material = UnityEngine.Material;

[GenerateAuthoringComponent]
public struct GhostPlacementBuilder : IComponentData
{
    public Grid TargetGrid;
    public int3 CurrentSlot;
    public float Range;
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
            Build(currentGrid, true);
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

            if (!rotating)
            {
                ghostId += sign;
                ghostId = ghostId.mod(builder.EntityPrefabs.Length);
                EntityManager.DestroyEntity(GetSingletonEntity<GhostPlacementBuilder>());
                Build(currentGrid, true);
                SetSingleton(bgp);
            }

            SetComponent(GetSingletonEntity<GhostPlacementBuilder>(), new UpdateRotation{Rotation = currentRotation});
        }
        else rotating = false;
    }

    public Entity Build(Entity gridEntity, bool asGhost)
    {
        if (!HasComponent<Grid>(gridEntity))
            return Entity.Null;

        var grid = GetComponent<Grid>(gridEntity);
        var entity = EntityManager.Instantiate(builder.EntityPrefabs[ghostId]);
        EntityManager.AddComponentData(entity, new Parent {Value = gridEntity});
        EntityManager.AddComponentData(entity, new UpdateRotation{Rotation = currentRotation});
        EntityManager.AddComponent<LocalToParent>(entity);
        EntityManager.AddComponent<CompositeRotation>(entity);
        EntityManager.AddComponent<RotationPivot>(entity);
        if (asGhost)
        {
            EntityManager.AddComponentData(entity, new GhostPlacementBuilder{
                TargetGrid = grid,
                CurrentSlot = new int3(int.MaxValue, int.MaxValue, int.MaxValue),
                Range = 3
            });

            uiRoot.Q("Current") .Q<Icon3D>().SetObject(builder.Prefabs[ghostId].gameObject);
            uiRoot.Q("Next")    .Q<Icon3D>().SetObject(builder.Prefabs[(ghostId + 1).mod(builder.Prefabs.Length)].gameObject);
            uiRoot.Q("Previous").Q<Icon3D>().SetObject(builder.Prefabs[(ghostId - 1).mod(builder.Prefabs.Length)].gameObject);

            uiRoot.Q("Current") .Q<Label>().text = builder.Prefabs[ghostId].gameObject.name;
            uiRoot.Q("Next")    .Q<Label>().text = builder.Prefabs[(ghostId + 1).mod(builder.Prefabs.Length)].gameObject.name;
            uiRoot.Q("Previous").Q<Label>().text = builder.Prefabs[(ghostId - 1).mod(builder.Prefabs.Length)].gameObject.name;
        }

        return entity;
    }
}

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
        foreach (var child in bfe.GetAllChildren(GetSingletonEntity<GhostPlacementBuilder>()))
        {
            if (EntityManager.HasComponent<RenderMesh>(child))
            {
                var rm = EntityManager.GetSharedComponentData<RenderMesh>(child);
                rm.material = settings.GhostPlacementMaterial;
                buffer.SetSharedComponent(child, rm);
            }

            if (EntityManager.HasComponent<PhysicsCollider>(child))
            {
                buffer.RemoveComponent<PhysicsCollider>(child);
            }
        }
    }
}

[CreateAssetMenu(menuName = "GhostPlacementSettings")]
public class GhostPlacementSettings : ScriptableObject
{
    public Material GhostPlacementMaterial;
    public Material GhostRemovalMaterial;

    public Material GridBuildMaterial;
    public Material GridDestroyMaterial;
}
