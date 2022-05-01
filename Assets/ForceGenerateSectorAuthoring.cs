using Unity.Entities;
using UnityEngine;

public struct ForceGenerateSector : IComponentData
{
    public ulong Sector;
}

public class ForceGenerateSectorAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public byte[] Sectors;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var target = 0ul;
        for (int i = Sectors.Length - 1; i >= 0; i--)
            target = (target << 4) + Sectors[i];
        dstManager.AddComponentData(entity, new ForceGenerateSector {Sector = target});
    }
}
