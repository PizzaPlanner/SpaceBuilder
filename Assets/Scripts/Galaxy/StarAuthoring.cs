using Unity.Entities;
using UnityEngine;

namespace Galaxy
{
    [RequireComponent(typeof(CelestialBodyAuthoring))]
    public class StarAuthoring : MonoBehaviour
    {
        class Baker : Baker<StarAuthoring>
        {
            public override void Bake(StarAuthoring authoring)
            {
                var e = GetEntity(TransformUsageFlags.Renderable);
                AddComponent<Star>(e);
                // TODO: Should be done via URP authoring, but that is not attachable currently
                AddComponent<URPStarTemperature>(e);
                AddComponent<URPStarFlareIntensity>(e);
            }
        }
    }
}