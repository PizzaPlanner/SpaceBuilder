using Unity.Entities;
using UnityEngine;

public class PlaceBuildingItemAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public BuildingPart[] Parts;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var buffer = dstManager.AddBuffer<PlaceBuildingItem>(entity);
        foreach (var part in Parts)
            buffer.Add(new PlaceBuildingItem{ PrefabId = part.GetComponent<IdentifieableAuthoring>().Id });
    }
}
