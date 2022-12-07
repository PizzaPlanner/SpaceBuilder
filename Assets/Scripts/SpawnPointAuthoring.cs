using Unity.Entities;
using UnityEngine;

public class SpawnPointAuthoring : MonoBehaviour
{
    public GameObject Prefab;

    class SpawnPointBaker : Baker<SpawnPointAuthoring>
    {
        public override void Bake(SpawnPointAuthoring authoring)
        {
            AddComponent(new SpawnPoint{Prefab = GetEntity(authoring.Prefab)});
        }
    }
}