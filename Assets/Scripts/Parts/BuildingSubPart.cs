using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct GridSubItem : IComponentData
{
    public int3 RelativeCell;
}

[RequireComponent(typeof(IdentifieableAuthoring))]
public class BuildingSubPart : MonoBehaviour, IConvertGameObjectToEntity
{
    public int3 Cell;
    private BuildingPart Parent;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new GridSubItem
        {
            RelativeCell = Cell
        });
        dstManager.AddComponentData(entity, new RotationPivot {Value = Grid.CELL_SIZE * 0.5f});
        dstManager.AddComponent<CompositeRotation>(entity);
    }

    void Start()
    {
        Parent = GetComponentInParent<BuildingPart>();
    }

    private void OnValidate()
    {
        transform.position = Grid.PositionFromCell(Cell);
    }

    private void OnDrawGizmos()
    {
        var size = Grid.CELL_SIZE;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(new float3(0.5f * size) + (float3)Cell * size, new float3(size));
    }
}
