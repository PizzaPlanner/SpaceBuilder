using Grid.Logic;
using Unity.Entities;
using UnityEngine;

public class GalaxyCompassAuthoring : MonoBehaviour
{
    class Baker : Baker<GalaxyCompassAuthoring>
    {
        public override void Bake(GalaxyCompassAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<GalaxyCompass>(e);
        }
    }
}