using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

[BakingType]
public class IdentifieableLoader : MonoBehaviour
{
    class Baker : Baker<IdentifieableLoader>
    {
        public override void Bake(IdentifieableLoader authoring)
        {
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
                    if(id.Id == 0)
                        Debug.LogError($"{go.name} has invalid ID 0!");
                    else
                    {
                        RegisterPrefabForBaking(go);
                        GetEntity(go);
                        prefabs++;
                    }
                }
            }
            Debug.Log($"Created {prefabs} Identifieables");

            
        }
    }
}
