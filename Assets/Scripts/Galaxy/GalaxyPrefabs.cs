using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class GalaxyPrefabs : MonoBehaviour
{
    [SerializeField] public GameObject[] Prefabs;
}

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class GalaxyPrefabsInitializationSystem : SystemBase
{
    private BlobAssetStore blobStore;
    private GameObjectConversionSettings settings;

    protected override void OnCreate()
    {
        blobStore = new BlobAssetStore();
        settings = GameObjectConversionSettings.FromWorld(World, blobStore);
        var builder = GameObject.FindObjectOfType<GalaxyPrefabs>();

        for (int i = 0; i < builder.Prefabs.Length; i++)
        {
            var e = GameObjectConversionUtility.ConvertGameObjectHierarchy(builder.Prefabs[i].gameObject, settings);
        }
    }

    protected override void OnDestroy()
    {
        blobStore.Dispose();
    }

    protected override void OnUpdate()
    {

    }
}
