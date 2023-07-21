using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR
[RequireComponent(typeof(IdentifieableAuthoring))]
public class BuildingSubPartAuthoring : MonoBehaviour
{
    public int3 Cell;
    private BuildingPartAuthoring Parent;

    class Baker : Baker<BuildingSubPartAuthoring>
    {
        public override void Bake(BuildingSubPartAuthoring authoring)
        {
            AddComponent(new GridSubItem
            {
                RelativeCell = authoring.Cell
            });

            // TODO: How ??
            // AddComponent(new RotationPivot {Value = Grid.CELL_SIZE * 0.5f});
            // AddComponent<CompositeRotation>(entity);
        }
    }

    void Start()
    {
        Parent = GetComponentInParent<BuildingPartAuthoring>();
    }

    private void OnValidate()
    {
        transform.position = GridStructure.PositionFromCell(Cell);
    }

    private void OnDrawGizmos()
    {
        var size = GridStructure.CELL_SIZE;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(new float3(0.5f * size) + (float3)Cell * size, new float3(size));
    }
}
#endif