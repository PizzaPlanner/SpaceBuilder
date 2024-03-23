#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class IdentifieableAuthoring : MonoBehaviour
{
    [SerializeField][HideInInspector]ulong ItemId;
    public Identifieable Id => new Identifieable(ItemId);
    
    public IdTag Tags;
    public Texture2D Icon;

    private void OnValidate()
    {
        if (!UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this) || gameObject.transform.parent != null)
            return;

        var guid = UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(gameObject));
        if (guid.Length < 16) return;
        ItemId = ulong.Parse(guid.Substring(0,16),System.Globalization.NumberStyles.HexNumber);
    }

    public Identifieable GetSubIdentifiable(int index)
    {
        var current = Id;
        var hash = math.hash(new uint2(current.ItemId, (uint)index));
        return new Identifieable(hash);
    }

    public class Baker : Baker<IdentifieableAuthoring>
    {
        public override void Bake(IdentifieableAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.None);
            AddComponent(e, new PreIdentifiable
            {
                Id = authoring.Id,
                Tags = authoring.Tags,
                Name = authoring.gameObject.name
            });
            AddComponentObject(e, new IconReference{Icon = authoring.Icon});
        }
    }
}
#endif
