using Unity.Entities;
using UnityEngine;

public class CelestialBodyAuthoring : MonoBehaviour
{
    public class CelestialBodyAuthoringBaker : Baker<CelestialBodyAuthoring>
    {
        public override void Bake(CelestialBodyAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Renderable);
            AddComponent<CelestialBody>(entity);
            AddComponent<GenerateIntoSector>(entity);
            AddComponent<ShouldShowAsPOI>(entity);
            SetComponentEnabled<ShouldShowAsPOI>(entity, false);
        }
    }
}