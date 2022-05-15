using Rival;
using Unity.Entities;
using UnityEngine;

public class GridAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public Grid.Size Size;
    public bool HasAirflow;
    public TextAsset Blueprint;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new Grid{MaxSize = Size});
        if(HasAirflow)
            dstManager.AddBuffer<AirtightBuffer>(entity);
        dstManager.AddBuffer<OccupationBuffer>(entity);
        dstManager.AddComponent<TrackedTransform>(entity);
        if (Blueprint != null)
        {
            var bp = BlueprintUtils.Load(Blueprint.text);
            dstManager.AddComponentData(entity, new Name {Value = bp.Name});
            foreach (var part in bp.Parts)
            {
                var e = conversionSystem.CreateAdditionalEntity(this);
                dstManager.AddComponentData(e, new PendingPart{Data = part, Grid = entity});
            }
        }
    }
}
