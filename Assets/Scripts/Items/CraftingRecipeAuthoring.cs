using Unity.Entities;
using UnityEngine;

public struct CraftingRecipe : IComponentData
{
    public float Duration;
}

public struct CraftingInput : IBufferElementData
{
    public long ItemId;
    public ushort Amount;
}

public struct CraftingOutput : IBufferElementData
{
    public long ItemId;
    public ushort Amount;
}

public struct CraftingCatalyst : IBufferElementData
{
    public long ItemId;
}

public class CraftingRecipeAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    [System.Serializable]
    public struct ItemWithAmount
    {
        public ItemAuthoring Prefab;
        public ushort Amount;
    }

    public float Duration;
    public ItemAuthoring[] Catalysts;
    public ItemWithAmount[] Input;
    public ItemWithAmount[] Output;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new CraftingRecipe {Duration = Duration});
        var inBuffer = dstManager.AddBuffer<CraftingInput>(entity);
        foreach (var thing in Input)
        {
            inBuffer.Add(new CraftingInput
                {Amount = thing.Amount, ItemId = thing.Prefab.GetComponent<IdentifieableAuthoring>().Id});
        }

        var outBuffer = dstManager.AddBuffer<CraftingOutput>(entity);
        foreach (var thing in Output)
        {
            outBuffer.Add(new CraftingOutput
                {Amount = thing.Amount, ItemId = thing.Prefab.GetComponent<IdentifieableAuthoring>().Id});
        }

        var catBuffer = dstManager.AddBuffer<CraftingCatalyst>(entity);
        foreach (var thing in Output)
        {
            catBuffer.Add(new CraftingCatalyst
                {ItemId = thing.Prefab.GetComponent<IdentifieableAuthoring>().Id});
        }
    }
}
