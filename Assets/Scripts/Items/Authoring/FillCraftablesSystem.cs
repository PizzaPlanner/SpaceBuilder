using Unity.Collections;
using Unity.Entities;
using UnityEngine;

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
                typeof(CraftingCatalyst),
                typeof(PreIdentifiable)
            },
            Options = EntityQueryOptions.IncludePrefab
        });
        RequireForUpdate<DefaultIdentifiableLink>();
    }

    protected override void OnUpdate()
    {
        var links = SystemAPI.GetSingletonBuffer<DefaultIdentifiableLink>(false);
        var allRecipes = query.ToEntityArray(Allocator.Temp);
        foreach(var (crafter, preId) in SystemAPI.Query<CrafterAspect, PreIdentifiable>()
                                                 .WithAll<Prefab>()
                                                 .WithOptions(EntityQueryOptions.IncludePrefab))
        {
            var recipes = crafter.Recipes;
            recipes.Clear();
            
            foreach (var recipe in allRecipes)
            {
                var recipeId = SystemAPI.GetComponent<PreIdentifiable>(recipe);
                var canCraft = true;
                var catalysts = SystemAPI.GetBuffer<CraftingCatalyst>(recipe);
                foreach(var cat in catalysts)
                    canCraft &= crafter.HasCatalyst(cat.ItemId);
                if (canCraft)
                {
                    recipes.Add(new Craftables{Recipe = recipeId.Id});
                    links.Add(new DefaultIdentifiableLink{From = preId.Id, To = recipeId.Id, RelationFrom = IdLinkType.Outputs, RelationTo = IdLinkType.CraftedIn});
                }
            }
        }
    }
}