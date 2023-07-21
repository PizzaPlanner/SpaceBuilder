using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class CelestialBodyAuthoring : MonoBehaviour
{
    public class CelestialBodyAuthoringBaker : Baker<CelestialBodyAuthoring>
    {
        public override void Bake(CelestialBodyAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.WorldSpace);
            AddComponent<CelestialBody>(entity);
            AddComponent<GenerateIntoSector>(entity);
            AddComponent<ShouldShowAsPOI>(entity);
            SetComponentEnabled<ShouldShowAsPOI>(entity, false);
        }
    }
}