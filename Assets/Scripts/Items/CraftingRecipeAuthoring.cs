using System;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using UnityEngine;

public struct Crafter : IComponentData { }

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

public class CraftingRecipeAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    [System.Serializable]
    public struct ItemWithAmount
    {
        public ItemAuthoring Prefab;
        public ushort Amount;
    }

    public float Duration;
    public ItemWithAmount[] Input;
    public ItemWithAmount[] Output;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new CraftingRecipe {Duration = Duration});
        var inBuffer = dstManager.AddBuffer<CraftingInput>(entity);
        var outBuffer = dstManager.AddBuffer<CraftingOutput>(entity);
        foreach (var thing in Input)
        {
            conversionSystem.DeclareAssetDependency(gameObject, thing.Prefab.gameObject);
            inBuffer.Add(new CraftingInput
                {Amount = thing.Amount, ItemId = thing.Prefab.GetComponent<IdentifieableAuthoring>().Id});
        }

        foreach (var thing in Output)
        {
            conversionSystem.DeclareAssetDependency(gameObject, thing.Prefab.gameObject);
            outBuffer.Add(new CraftingOutput
                {Amount = thing.Amount, ItemId = thing.Prefab.GetComponent<IdentifieableAuthoring>().Id});
        }
    }
}
