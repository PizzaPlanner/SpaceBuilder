using Unity.Entities;
using UnityEngine;

public class ItemAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public int MaxStackSize;
    public GameObject WorldObject;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new Item{WorldRepresentation = conversionSystem.GetPrimaryEntity(WorldObject)});
        if (MaxStackSize > 0)
            dstManager.AddComponentData(entity, new Stackable {MaxSize = (ushort)MaxStackSize});
    }
}
