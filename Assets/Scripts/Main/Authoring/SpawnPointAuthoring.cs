using Unity.Entities;
using UnityEngine;

public class SpawnPointAuthoring : MonoBehaviour
{
    public GameObject Prefab;

    class SpawnPointBaker : Baker<SpawnPointAuthoring>
    {
        public override void Bake(SpawnPointAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.Dynamic);
            var prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic);
            AddComponent(e, new SpawnPoint { Prefab = prefab });
        }
    }
}