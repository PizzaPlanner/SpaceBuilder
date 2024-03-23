#if UNITY_EDITOR
using System;
using Unity.Entities;
using UnityEngine;

public class CraftingRecipeAuthoring : MonoBehaviour
{
    [System.Serializable]
    public struct ItemWithAmount
    {
        public ItemAuthoring Prefab;
        [Range(1, 9999)]
        public ushort Amount;
    }

    public Texture2D Icon;
    public float Duration;
    public ItemAuthoring[] Catalysts;
    public ItemWithAmount[] Input;
    public ItemWithAmount[] Output;

    void OnValidate()
    {
        if(Output.Length == 1)
            Icon = Output[0].Prefab.Icon;
    }

    class Baker : Baker<CraftingRecipeAuthoring>
    {
        public override void Bake(CraftingRecipeAuthoring authoring)
        {
            var self = GetEntity(TransformUsageFlags.None);
            AddComponent(self, new CraftingRecipe {Duration = authoring.Duration});
            AddComponentObject(self, new IconReference {Icon = authoring.Icon});
            var inBuffer = AddBuffer<CraftingInput>(self);
            foreach (var thing in authoring.Input)
            {
                inBuffer.Add(new CraftingInput
                    {Amount = thing.Amount, ItemId = thing.Prefab.GetComponent<IdentifieableAuthoring>().Id});
            }

            var outBuffer = AddBuffer<CraftingOutput>(self);
            foreach (var thing in authoring.Output)
            {
                outBuffer.Add(new CraftingOutput
                    {Amount = thing.Amount, ItemId = thing.Prefab.GetComponent<IdentifieableAuthoring>().Id});
            }

            var catBuffer = AddBuffer<CraftingCatalyst>(self);
            foreach (var thing in authoring.Catalysts)
            {
                catBuffer.Add(new CraftingCatalyst
                    {ItemId = thing.GetComponent<IdentifieableAuthoring>().Id});
            }
        }
    }
}
#endif