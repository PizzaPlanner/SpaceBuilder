using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class BuildingPart : MonoBehaviour
{

    [SerializeField] [ReadOnly] private long ItemID;

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
        Gizmos.DrawWireCube(Vector3.one * 0.5f, Vector3.one);
    }
    #endif
}
