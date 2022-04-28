using Unity.Entities;
using UnityEngine;

public class ItemAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public ushort MaxStackSize;
    [Range(1, 9999)]
    public ushort Stack;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new Item{ });
        if (MaxStackSize > 0)
            dstManager.AddComponentData(entity, new Stackable {MaxSize = (ushort)MaxStackSize, Size = Stack});
    }
}
