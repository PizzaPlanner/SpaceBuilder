using Unity.Entities;
using UnityEngine;

public struct CraftingRecipe : IComponentData
{
    public float Duration;
}

public struct CraftingInput : IBufferElementData
{
    public ulong ItemId;
    public ushort Amount;
}

public struct CraftingOutput : IBufferElementData
{
    public ulong ItemId;
    public ushort Amount;
}

public struct CraftingCatalyst : IBufferElementData
{
    public ulong ItemId;
}

public class CraftingRecipeAuthoring : MonoBehaviour
{
    [System.Serializable]
    public struct ItemWithAmount
    {
        public ItemAuthoring Prefab;
        [Range(1, 9999)]
        public ushort Amount;
    }

    public float Duration;
    public ItemAuthoring[] Catalysts;
    public ItemWithAmount[] Input;
    public ItemWithAmount[] Output;

    class Baker : Baker<CraftingRecipeAuthoring>
    {
        public override void Bake(CraftingRecipeAuthoring authoring)
        {
            AddComponent(new CraftingRecipe {Duration = authoring.Duration});
            var inBuffer = AddBuffer<CraftingInput>();
            foreach (var thing in authoring.Input)
            {
                inBuffer.Add(new CraftingInput
                    {Amount = thing.Amount, ItemId = thing.Prefab.GetComponent<IdentifieableAuthoring>().Id});
            }

            var outBuffer = AddBuffer<CraftingOutput>();
            foreach (var thing in authoring.Output)
            {
                outBuffer.Add(new CraftingOutput
                    {Amount = thing.Amount, ItemId = thing.Prefab.GetComponent<IdentifieableAuthoring>().Id});
            }

            var catBuffer = AddBuffer<CraftingCatalyst>();
            foreach (var thing in authoring.Catalysts)
            {
                catBuffer.Add(new CraftingCatalyst
                    {ItemId = thing.GetComponent<IdentifieableAuthoring>().Id});
            }
        }
    }
}
