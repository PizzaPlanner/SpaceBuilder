using Unity.Entities;
using UnityEngine;

public class AsteroidAuthoring : MonoBehaviour
{
    class Baker : Baker<AsteroidAuthoring>
    {
        public override void Bake(AsteroidAuthoring authoring)
        {
            AddComponent<AsteroidData>();
        }
    }
}