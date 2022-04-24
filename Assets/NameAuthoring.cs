using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public struct Name : IComponentData
{
    public FixedString32Bytes Value;
}

public class NameAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public string Name;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new Name{Value = Name});
    }
}
