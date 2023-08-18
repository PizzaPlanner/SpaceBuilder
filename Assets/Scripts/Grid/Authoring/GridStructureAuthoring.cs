using Grid.Logic;
using Unity.Entities;
using Unity.Transforms;
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
            AddBuffer<BuildingPartInfo>(e);
            AddBuffer<LinkedEntityGroup>(e).Add(e);
            AddBuffer<Child>(e);
            AddComponent<MoveableShip>(e);
            AddComponent<ShouldShowAsPOI>(e);
            SetComponentEnabled<ShouldShowAsPOI>(e, false);
        }
    }
}
