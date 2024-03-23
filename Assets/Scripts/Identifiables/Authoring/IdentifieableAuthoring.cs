#if UNITY_EDITOR
using Unity.Entities;
using UnityEngine;

public class IdentifieableAuthoring : MonoBehaviour
{
    [SerializeField][HideInInspector]ulong ItemId;
    public Identifieable Id => new Identifieable(ItemId);

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
            var e = GetEntity(TransformUsageFlags.None);
            AddComponent(e, authoring.Id);
            AddComponent(e, new DefaultName{Value = authoring.gameObject.name});
        }
    }
}
#endif
