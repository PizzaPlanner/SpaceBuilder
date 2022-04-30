using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class ConstructionWandAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var query = dstManager.CreateEntityQuery(typeof(Identifieable), typeof(Prefab), typeof(GridItem));
        var buffer = dstManager.AddBuffer<BuildableBuffer>(entity);
        foreach (var part in query.ToComponentDataArray<Identifieable>(Allocator.Temp))
            buffer.Add(new BuildableBuffer{ PrefabId = part.ItemId });
        query.Dispose();
    }
}
