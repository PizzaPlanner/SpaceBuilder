using Unity.Entities;

public readonly partial struct RecipeAspect : IAspect
{
    public readonly Entity Entity;
    readonly RefRO<CraftingRecipe> _recipe;
    readonly RefRO<Identifieable> _id;
    public readonly DynamicBuffer<CraftingInput> Inputs;
    public readonly DynamicBuffer<CraftingOutput> Outputs;
    public readonly DynamicBuffer<CraftingCatalyst> Catalysts;
    public float Duration => _recipe.ValueRO.Duration;
    public Identifieable Id => _id.ValueRO;
}

public struct CraftingRecipe : IComponentData
{
    public float Duration;
}

public struct CraftingInput : IBufferElementData
{
    public Identifieable ItemId;
    public ushort Amount;
}

public struct CraftingOutput : IBufferElementData
{
    public Identifieable ItemId;
    public ushort Amount;
}

public struct CraftingCatalyst : IBufferElementData
{
    public Identifieable ItemId;
}