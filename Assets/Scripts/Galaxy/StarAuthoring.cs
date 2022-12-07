using Unity.Entities;
using UnityEngine;

namespace Galaxy
{
    public class StarAuthoring : MonoBehaviour
    {
        class Baker : Baker<StarAuthoring>
        {
            public override void Bake(StarAuthoring authoring)
            {
                AddComponent<Star>();
            }
        }
    }
}