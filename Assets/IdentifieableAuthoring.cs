using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public struct Identifieable : IComponentData
{
    public long ItemId;
}

public class IdentifieableAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    [SerializeField][HideInInspector]long ItemId;
    public long Id => ItemId;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new Identifieable {ItemId = ItemId});
    }
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this) || gameObject.transform.parent != null)
            return;

        var guid = UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(gameObject));
        if (guid.Length < 16) return;
        ItemId = long.Parse(guid.Substring(0,16),System.Globalization.NumberStyles.HexNumber);
    }
#endif
}
