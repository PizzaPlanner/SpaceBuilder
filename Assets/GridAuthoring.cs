using Rival;
using Unity.Entities;
using UnityEngine;

public class GridAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public Grid.Size Size;
    public bool HasAirflow;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new Grid{MaxSize = Size});
        if(HasAirflow)
            dstManager.AddBuffer<AirtightBuffer>(entity);
        dstManager.AddBuffer<OccupationBuffer>(entity);
        dstManager.AddComponent<TrackedTransform>(entity);
    }
}
