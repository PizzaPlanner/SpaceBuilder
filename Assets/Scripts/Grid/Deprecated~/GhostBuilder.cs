using System;
using UnityEngine;

public class GhostBuilder : MonoBehaviour
{
    [SerializeField] WireframeDisplay WireframePrefab;

    [NonSerialized] public static WireframeDisplay Wireframe;

    void Awake()
    {
        Wireframe = Instantiate(WireframePrefab);
        Wireframe.gameObject.SetActive(false);
    }
}
