using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct ElectricityProvider : IComponentData
{
    public float Value;
    public Entity Network;
}

public class ElectricityProviderAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public float Value;
    public Grid.Size Size;
    public int3 Cell;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new ElectricityProvider {Value = Value});
    }

    private void OnValidate()
    {
        transform.position = Grid.PositionFromCell(Size, Cell) + new float3(0.5f * Grid.CellSize(Size));
    }

    private void OnDrawGizmos()
    {
        var size = Grid.CellSize(Size);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(new float3(0.5f * size) + (float3)Cell * size, new float3(size));
    }
}
