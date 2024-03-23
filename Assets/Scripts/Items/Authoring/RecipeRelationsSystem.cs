#if UNITY_EDITOR
using Unity.Entities;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
partial struct RecipeRelationsSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DefaultIdentifiableLink>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var links = SystemAPI.GetSingletonBuffer<DefaultIdentifiableLink>(false);
        foreach (var (inputs, outputs, preId) in
                 SystemAPI.Query<DynamicBuffer<CraftingInput>, DynamicBuffer<CraftingOutput>, PreIdentifiable>()
                          .WithAll<Prefab>()
                          .WithOptions(EntityQueryOptions.IncludePrefab))
        {
            foreach (var input in inputs)
                links.Add(new DefaultIdentifiableLink{To = input.ItemId, From = preId.Id, RelationFrom = IdLinkType.RecipeInput, RelationTo = IdLinkType.UsedIn});
            foreach(var output in outputs)
                links.Add(new DefaultIdentifiableLink{To = output.ItemId, From = preId.Id, RelationFrom = IdLinkType.Outputs, RelationTo = IdLinkType.FromRecipe});
        }
    }
}
#endif