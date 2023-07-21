using Unity.Entities;
using UnityEngine;

[RequireComponent(typeof(LocalToGalaxyAuthoring))]
public class GridStructureAuthoring : MonoBehaviour
{
    class Baker : Baker<GridStructureAuthoring>
    {
        public override void Bake(GridStructureAuthoring structureAuthoring)
        {
            var e = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<GridStructure>(e);
            AddComponent<GridUpdate>(e);
            SetComponentEnabled<GridUpdate>(e, false);
            AddBuffer<Rooms>(e);
            AddBuffer<BuildingPartInfo>(e);
            AddComponent<ShouldShowAsPOI>(e);
            SetComponentEnabled<ShouldShowAsPOI>(e, false);
        }
    }
}
