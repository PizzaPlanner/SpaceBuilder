using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class GhostBuilder : MonoBehaviour
{
    [SerializeField] public IdentifieableAuthoring[] Prefabs;
    [SerializeField] public WireframeDisplay WireframePrefab;
}

public struct BuildableBuffer : IBufferElementData
{
    public Entity Prefab;
}

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class BuildingPartManagementSystem : SystemBase
{
    private BlobAssetStore blobStore;
    private GameObjectConversionSettings settings;

    public static WireframeDisplay WireframeDisplay;
    public static NativeHashMap<long, Entity> BuildingPartById;

    protected override void OnCreate()
    {
        blobStore = new BlobAssetStore();
        settings = GameObjectConversionSettings.FromWorld(World, blobStore);
        var builder = GameObject.FindObjectOfType<GhostBuilder>();
        WireframeDisplay = GameObject.Instantiate(builder.WireframePrefab);

        BuildingPartById = new NativeHashMap<long, Entity>(builder.Prefabs.Length, Allocator.Persistent);
        for (int i = 0; i < builder.Prefabs.Length; i++)
        {
            var e = GameObjectConversionUtility.ConvertGameObjectHierarchy(builder.Prefabs[i].gameObject, settings);
            BuildingPartById.Add(GetComponent<Identifieable>(e).ItemId, e);
        }
    }

    protected override void OnDestroy()
    {
        blobStore.Dispose();
        BuildingPartById.Dispose();
    }

    protected override void OnUpdate()
    {

    }
}
