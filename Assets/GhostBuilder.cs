using System;
using Unity.Entities;
using UnityEngine;

public class GhostBuilder : MonoBehaviour
{
    [SerializeField] BuildingPart[] Prefabs;
    [SerializeField] WireframeDisplay WireframePrefab;
    public Entity[] EntityPrefabs;
    public static WireframeDisplay WireframeDisplay;

    public void MakePrefabs(GameObjectConversionSettings settings)
    {
        WireframeDisplay = Instantiate(WireframePrefab);
        EntityPrefabs = new Entity[Prefabs.Length];
        for(int i = 0; i < Prefabs.Length; i++)
            EntityPrefabs[i] =GameObjectConversionUtility.ConvertGameObjectHierarchy(Prefabs[i].gameObject, settings);
    }
}
