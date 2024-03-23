using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class CrafterAuthoring : MonoBehaviour
{
    public InventoryAuthoring Input, Output;
    public IdentifieableAuthoring[] Catalysts;

    class Baker : Baker<CrafterAuthoring>
    {
        public override void Bake(CrafterAuthoring authoring)
        {
            var self = GetEntity(TransformUsageFlags.None);
            AddComponent(self, new Crafter 
            {
                InputInventory = GetEntity(authoring.Input, TransformUsageFlags.None),
                OutputInventory = GetEntity(authoring.Output, TransformUsageFlags.None)
            });
            AddBuffer<CraftingQueue>(self);
            AddBuffer<Craftables>(self);
            var catalysts = AddBuffer<CatalystInventory>(self);
            foreach (var item in authoring.Catalysts)
            {
                catalysts.Add(new CatalystInventory{ ItemId = item.Id });
            }
        }
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
partial class FillCraftablesSystem : SystemBase
{
    EntityQuery query;
    protected override void OnCreate()
    {
        query = GetEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[]{typeof(Prefab), 
                typeof(CraftingRecipe), 
                typeof(CraftingCatalyst)},
            Options = EntityQueryOptions.IncludePrefab
        });
    }

    protected override void OnUpdate()
    {
        var allRecipes = query.ToEntityArray(Allocator.Temp);
        foreach(var crafter in SystemAPI.Query<CrafterAspect>().WithAll<Prefab>().WithOptions(EntityQueryOptions.IncludePrefab))
        {
            var recipes = crafter.Recipes;
            recipes.Clear();
            
            foreach (var recipe in allRecipes)
            {
                var canCraft = true;
                var catalysts = SystemAPI.GetBuffer<CraftingCatalyst>(recipe);
                foreach(var cat in catalysts)
                    canCraft &= crafter.HasCatalyst(cat.ItemId);
                if(canCraft)
                    recipes.Add(new Craftables{Recipe = SystemAPI.GetComponent<Identifieable>(recipe)});
            }
        }
    }
}
