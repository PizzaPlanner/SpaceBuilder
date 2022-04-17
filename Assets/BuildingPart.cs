using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public struct GridItem : IComponentData
{
    public long ItemId;
    public float Size;
}

public class BuildingPart : MonoBehaviour, IConvertGameObjectToEntity
{

    [SerializeField] [ReadOnly] private long ItemID;
    [SerializeField] private float Size;
    [SerializeField] public Texture2D Icon;

    #if UNITY_EDITOR
    private void OnValidate()
    {
        if (!UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this))
            return;

        var guid = UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(gameObject));
        ItemID = long.Parse(guid[..16],System.Globalization.NumberStyles.HexNumber);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireCube(Vector3.one * 0.5f * Size, Vector3.one * Size);
    }
    #endif

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new GridItem {Size = Size, ItemId = ItemID});
    }
}
