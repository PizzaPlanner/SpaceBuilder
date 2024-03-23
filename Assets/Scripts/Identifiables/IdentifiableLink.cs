using Unity.Entities;

public enum IdLinkType : byte
{
    Unknown,
    UsedIn,
    FromRecipe,
    RecipeInput,
    Outputs,
    CraftedIn,
}

public struct IdentifiableLink : IComponentData
{
    public Identifieable From, To;
    public IdLinkType Relation;
}