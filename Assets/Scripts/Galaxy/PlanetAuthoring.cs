using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

// [RequireComponent(typeof(URPMaterialPropertyBaseColorAuthoring))]
public class PlanetAuthoring : MonoBehaviour
{
    class Baker : Baker<PlanetAuthoring>
    {
        public override void Bake(PlanetAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.Renderable);
            AddComponent<Planet>(e);
            AddComponent<CelestialBody>(e);
            // TODO: Should be done via URP authoring, but that is not attachable currently
            AddComponent<URPMaterialPropertyBaseColor>(e);
        }
    }
}