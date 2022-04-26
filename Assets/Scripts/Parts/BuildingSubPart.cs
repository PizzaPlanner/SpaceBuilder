using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct GridSubItem : IComponentData
{
    public int3 RelativeCell;
    public Grid.Size Size;
}

[RequireComponent(typeof(IdentifieableAuthoring))]
public class BuildingSubPart : MonoBehaviour, IConvertGameObjectToEntity
{
    public Grid.Size Size;
    public int3 Cell;
    private BuildingPart Parent;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new GridSubItem
        {
            Size = Size,
            RelativeCell = Cell
        });
        dstManager.AddComponentData(entity, new RotationPivot {Value = Grid.CellSize(Size) * 0.5f});
        dstManager.AddComponent<CompositeRotation>(entity);
        dstManager.AddBuffer<ConnectionBuffer>(entity);
    }

    void Start()
    {
        Parent = GetComponentInParent<BuildingPart>();
    }

    private void OnValidate()
    {
        transform.position = Grid.PositionFromCell(Size, Cell);
    }

    private void OnDrawGizmos()
    {
        var size = Grid.CellSize(Size);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(new float3(0.5f * size) + (float3)Cell * size, new float3(size));
    }
}
