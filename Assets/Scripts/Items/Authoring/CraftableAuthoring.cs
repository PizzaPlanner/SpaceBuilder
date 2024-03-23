#if UNITY_EDITOR
using System;
using Unity.Entities;
using UnityEngine;

[RequireComponent(typeof(ItemAuthoring))]
public class CraftableAuthoring : MonoBehaviour
{
    public SimpleRecipe[] Recipes;
    
    [Serializable]
    public class SimpleRecipe
    {
        [Range(1, 9999)]
        public ushort Amount;
        public float Duration;
        public IdentifieableAuthoring Catalyst;
        public CraftingRecipeAuthoring.ItemWithAmount[] Input;   
    }

    class Baker : Baker<CraftableAuthoring>
    {
        public override void Bake(CraftableAuthoring authoring)
        {
            var itemId = GetComponent<IdentifieableAuthoring>(authoring);
            for (int i = 0; i < authoring.Recipes.Length; i++)
            {
                var recSimple = authoring.Recipes[i];
                var recipe = CreateAdditionalEntity(TransformUsageFlags.None);
                AddComponent<Prefab>(recipe);
                AddComponent(recipe, new PreIdentifiable
                {
                    Name = $"{authoring.gameObject.name}_AutoRecipe{i}",
                    Id = itemId.GetSubIdentifiable(i),
                    Tags = IdTag.Recipe
                });
                AddComponentObject(recipe, new IconReference{Icon = itemId.Icon});

                AddComponent(recipe, new CraftingRecipe
                {
                    Duration = recSimple.Duration
                });
                var outBuffer = AddBuffer<CraftingOutput>(recipe);
                outBuffer.Add(new CraftingOutput
                    {Amount = recSimple.Amount, ItemId = itemId.Id});
                
                var inBuffer = AddBuffer<CraftingInput>(recipe);
                foreach (var thing in recSimple.Input)
                {
                    inBuffer.Add(new CraftingInput
                        {Amount = thing.Amount, ItemId = thing.Prefab.GetComponent<IdentifieableAuthoring>().Id});
                }

                var catBuffer = AddBuffer<CraftingCatalyst>(recipe);
                catBuffer.Add(new CraftingCatalyst {ItemId = recSimple.Catalyst.Id});
            }
        }
    }
}
#endif