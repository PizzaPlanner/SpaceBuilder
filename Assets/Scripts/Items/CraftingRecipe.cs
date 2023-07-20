using Unity.Entities;

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