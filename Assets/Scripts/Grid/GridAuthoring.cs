using Rival;
using Unity.Entities;
using UnityEngine;

[RequireComponent(typeof(LocalToGalaxyAuthoring))]
public class GridAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public string FilePath;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new ExecuteAdminScript {Script = $"spawn \"{FilePath}\"", Executor = entity});
    }
}
