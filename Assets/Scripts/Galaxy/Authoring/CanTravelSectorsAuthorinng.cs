using Galaxy.SectorLoading;
using Unity.Entities;
using UnityEngine;

namespace Galaxy
{
    [RequireComponent(typeof(LocalToGalaxyAuthoring))]
    public class CanTravelSectorsAuthorinng : MonoBehaviour
    {
        class TravelBaker : Baker<CanTravelSectorsAuthorinng>
        {
            public override void Bake(CanTravelSectorsAuthorinng authoring)
            {
                var e = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<CanTravelSectors>(e);
            }
        }
    }
}