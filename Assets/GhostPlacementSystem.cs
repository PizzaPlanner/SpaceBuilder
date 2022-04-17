using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

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

    protected override void OnCreate()
    {
        blobStore = new BlobAssetStore();
        settings = GameObjectConversionSettings.FromWorld(World, blobStore);
        builder = GameObject.FindObjectOfType<GhostBuilder>();
        builder.MakePrefabs(settings);

        currentRotation = quaternion.identity;

        RequireSingletonForUpdate<LocalPlayer>();
    }

    protected override void OnStartRunning()
    {
        uiRoot = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("BuildMenu");
        uiRoot.style.display = DisplayStyle.None;
    }

    protected override void OnDestroy()
    {
        blobStore.Dispose();
    }

    protected override void OnUpdate()
    {
        var currentGrid = GetComponent<Parent>(GetSingletonEntity<LocalPlayer>()).Value;
        var hasbuilder = HasSingleton<GhostPlacementBuilder>();
        uiRoot.style.display = hasbuilder ? DisplayStyle.Flex : DisplayStyle.None;
        if (hasbuilder)
        {
            if (!HasComponent<Grid>(currentGrid) || Input.GetKeyDown(KeyCode.B))
            {
                EntityManager.DestroyEntity(GetSingletonEntity<GhostPlacementBuilder>());
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

            uiRoot.Q<Icon3D>("ObjectToBuild").SetObject(builder.Prefabs[ghostId].gameObject);
            uiRoot.Q<Icon3D>("Next").SetObject(builder.Prefabs[(ghostId + 1).mod(builder.Prefabs.Length)].gameObject);
            uiRoot.Q<Icon3D>("Previous").SetObject(builder.Prefabs[(ghostId - 1).mod(builder.Prefabs.Length)].gameObject);
            uiRoot.Q<Label>("ItemName").text = builder.Prefabs[ghostId].gameObject.name;

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
