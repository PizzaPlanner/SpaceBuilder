using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class LocalToGalaxyAuthoring : MonoBehaviour
{
    public int3 LightSector;
    public int3 StarSector;
    public int3 PlanetSector;
    public double3 LocalPosition;

    class Baker : Baker<LocalToGalaxyAuthoring>
    {
        public override void Bake(LocalToGalaxyAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.None);
            AddComponent(e, new LocalToGalaxy
            {
                LightSector   = authoring.LightSector,
                StarSector   = authoring.StarSector,
                PlanetSector   = authoring.PlanetSector,
                LocalPosition   = authoring.LocalPosition,
            });
            AddComponent<TooFarAway>(e);
        }
    }
}