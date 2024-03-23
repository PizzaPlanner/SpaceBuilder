#if UNITY_EDITOR
using Unity.Entities;
using UnityEngine;

public class IdentifieableLoader : MonoBehaviour
{
    [SerializeField] GameObject[] AdditionalPrefabs;
    class Baker : Baker<IdentifieableLoader>
    {
        public override void Bake(IdentifieableLoader authoring)
        {
            var e = GetEntity(TransformUsageFlags.None);
            AddBuffer<DefaultIdentifiableLink>(e);
            var prefabs = 0;
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("*", new[] { "Assets/Identifieables" }))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".prefab") || path.Contains("__TEMPLATE__"))
                    continue;

                var obj = UnityEditor.AssetDatabase.LoadMainAssetAtPath(path);
                if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(obj) && obj is GameObject go &&
                    go.TryGetComponent(out IdentifieableAuthoring id))
                {
                    if(id.Id == default)
                        Debug.LogError($"{go.name} has invalid ID 0!");
                    else
                    {
                        RegisterPrefabForBaking(go);
                        GetEntity(go, TransformUsageFlags.None);
                        prefabs++;
                    }
                }
            }
            Debug.Log($"Created {prefabs} Identifieables");

            foreach (var prefab in authoring.AdditionalPrefabs)
            {
                RegisterPrefabForBaking(prefab);
                GetEntity(prefab, TransformUsageFlags.None);
            }
            Debug.Log($"Created {authoring.AdditionalPrefabs.Length} Prefabs");
        }
    }
}
#endif