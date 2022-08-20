using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class IdentifieableLoader : MonoBehaviour, IDeclareReferencedPrefabs
{
#if !UNITY_EDITOR
    public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
    {
        Debug.Log("Nothing to see here");
    }
#endif
#if UNITY_EDITOR
    public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
    {
        var list = new List<GameObject>();
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
                    Debug.LogError($"{id.gameObject.name} has invalid ID 0!");
                else
                    list.Add(id.gameObject);
            }
        }
        Debug.Log($"Created {list.Count} Identifieables");

        referencedPrefabs.AddRange(list);
    }
#endif
}
