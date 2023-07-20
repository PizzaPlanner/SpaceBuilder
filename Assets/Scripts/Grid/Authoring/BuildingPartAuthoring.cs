using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

#if UNITY_EDITOR
[RequireComponent(typeof(IdentifieableAuthoring))]
public class BuildingPartAuthoring : MonoBehaviour
{
    [SerializeField] private uint3 SizeInGrid;
    [SerializeField] private bool Scalable;
    [SerializeField] private bool IsWall;
    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube((float3)SizeInGrid * 0.5f * GridStructure.CELL_SIZE, (float3)SizeInGrid * GridStructure.CELL_SIZE);
    }

    private void OnValidate()
    {
        if (!math.all(SizeInGrid == 1))
            Scalable = false;
    }

    class Baker : Baker<BuildingPartAuthoring>
    {
        public override void Bake(BuildingPartAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, new GridItem
            {
                Area = new BoundingBox{Origin = int3.zero, Extents = (int3)authoring.SizeInGrid - 1},
                Scalable = math.all(authoring.SizeInGrid == 1) && authoring.Scalable,
                IsWall = authoring.IsWall
            });
            // TODO
            // AddComponent(new RotationPivot {Value = Grid.CELL_SIZE * 0.5f});
            // AddComponent<CompositeRotation>();
            AddComponent<Parent>(entity);
        }
    }
}
#endif