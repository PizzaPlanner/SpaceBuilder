using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class LocalToGalaxyAuthoring : MonoBehaviour
{
    public int3 Sector;
    public int3 SubSector;
    public float3 LocalPosition;
    
    class Baker : Baker<LocalToGalaxyAuthoring>
    {
        public override void Bake(LocalToGalaxyAuthoring authoring)
        {
            AddComponent<LocalToGalaxy>(new LocalToGalaxy
            {
                Sector = authoring.Sector,
                SubSector = authoring.SubSector,
                LocalPosition = authoring.LocalPosition,
            });
        }
    }
}