using Unity.Entities;
using UnityEngine;

public class CrafterAuthoring : MonoBehaviour
{
    public CraftingRecipeAuthoring[] Recipes;
    public InventoryAuthoring Input, Output, Catalysts;

    class Baker : Baker<CrafterAuthoring>
    {
        public override void Bake(CrafterAuthoring authoring)
        {
        var queue = CreateAdditionalEntity();
        AddComponent(new Crafter {Queue = queue});
        AddComponent(queue, new CraftingQueue
        {
            InputInventory = GetEntity(authoring.Input),
            OutputInventory = GetEntity(authoring.Output),
            CatalystInventory = GetEntity(authoring.Catalysts)
        });
        AddBuffer<CraftingBuffer>(queue);
        var craftables = AddBuffer<CraftableBuffer>(queue);
        foreach (var recipe in authoring.Recipes)
            craftables.Add(new CraftableBuffer{Recipe = GetEntity(recipe.gameObject)});
        }
    }
}
