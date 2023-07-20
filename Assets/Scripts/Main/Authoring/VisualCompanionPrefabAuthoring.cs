using Unity.Entities;
using UnityEngine;

public class VisualCompanionPrefabAuthoring : MonoBehaviour
{
    GameObject Prefab;
    class Baker : Baker<VisualCompanionPrefabAuthoring>
    {
        public override void Bake(VisualCompanionPrefabAuthoring authoring)
        {
            AddComponentObject(new VisualCompanionPrefab { Prefab = authoring.Prefab });
        }
    }
}