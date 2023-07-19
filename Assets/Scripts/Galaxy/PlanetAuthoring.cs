using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

[RequireComponent(typeof(CelestialBodyAuthoring))]
public class PlanetAuthoring : MonoBehaviour
{
    class Baker : Baker<PlanetAuthoring>
    {
        public override void Bake(PlanetAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.Renderable);
            AddComponent<Planet>(e);
            // TODO: Should be done via URP authoring, but that is not attachable currently
            AddComponent<URPMaterialPropertyBaseColor>(e);
        }
    }
}