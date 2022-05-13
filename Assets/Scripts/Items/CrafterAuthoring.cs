using Unity.Entities;
using UnityEngine;

public struct Crafter : IComponentData
{
    public Entity Queue;
}

public class CrafterAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public CraftingRecipeAuthoring[] Recipes;
    public InventoryAuthoring Input, Output, Catalysts;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var queue = conversionSystem.CreateAdditionalEntity(this);
        dstManager.AddComponentData(entity, new Crafter {Queue = queue});
        dstManager.AddComponentData(queue, new CraftingQueue
        {
            InputInventory = conversionSystem.GetPrimaryEntity(Input),
            OutputInventory = conversionSystem.GetPrimaryEntity(Output),
            CatalystInventory = conversionSystem.GetPrimaryEntity(Catalysts)
        });
        dstManager.AddBuffer<CraftingBuffer>(queue);
        var craftables = dstManager.AddBuffer<CraftableBuffer>(queue);
        foreach (var recipe in Recipes)
            craftables.Add(new CraftableBuffer{Recipe = conversionSystem.GetPrimaryEntity(recipe.gameObject)});
    }
}
