using Unity.Entities;
using UnityEngine;

public class ConvertToPrefabAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponent<Prefab>(entity);
    }
}
