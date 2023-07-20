#if UNITY_EDITOR
using Unity.Entities;
using UnityEngine;

public class IdentifieableAuthoring : MonoBehaviour
{
    [SerializeField][HideInInspector]ulong ItemId;
    public ulong Id => ItemId;

    private void OnValidate()
    {
        if (!UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this) || gameObject.transform.parent != null)
            return;

        var guid = UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(gameObject));
        if (guid.Length < 16) return;
        ItemId = ulong.Parse(guid.Substring(0,16),System.Globalization.NumberStyles.HexNumber);
    }

    class Baker : Baker<IdentifieableAuthoring>
    {
        public override void Bake(IdentifieableAuthoring authoring)
        {
            AddComponent(new Identifieable {ItemId = authoring.Id});
        }
    }
}
#endif
