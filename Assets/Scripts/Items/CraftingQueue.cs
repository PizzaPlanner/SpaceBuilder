using Unity.Entities;

public struct CraftingQueue : IComponentData
{
    public Entity InputInventory;
    public Entity OutputInventory;
    public Entity CatalystInventory;
}

public struct CraftableBuffer : IBufferElementData
{
    public Entity Recipe;
}

public struct CraftingBuffer : IBufferElementData
{
    public Entity Recipe;
    public float TimeLeft;
}

[UpdateAfter(typeof(InventoryManagementSystem))]
public partial class CraftingSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<IdentifiableMappingSystem.Loaded>();
    }

    protected override void OnUpdate()
    {
        var buffer = GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var deltaTime = World.Time.DeltaTime;
        var cBuffers = GetBufferLookup<CraftingCatalyst>(true);
        var iBuffers = GetBufferLookup<CraftingInput>(true);
        var oBuffers = GetBufferLookup<CraftingOutput>(true);
        var identifiables = GetComponentLookup<Identifieable>(true);
        var stackables = GetComponentLookup<Stackable>(true);

        Entities
            .WithReadOnly(identifiables)
            .WithReadOnly(stackables)
            .ForEach((Entity e, ref DynamicBuffer<CraftingBuffer> crafting, in CraftingQueue crafter) =>
        {
            if (crafting.Length == 0)
                return;

            var current = crafting[0];
            current.TimeLeft -= deltaTime;
            crafting[0] = current;

            if (cBuffers.HasComponent(current.Recipe))
            {
                var cInventory = GetBuffer<Inventory>(crafter.CatalystInventory);
                var cRecipe = cBuffers[current.Recipe];
            
                foreach (var requirement in cRecipe)
                {
                    bool hasReq = false;
                    foreach (var item in cInventory)
                        hasReq |= item.Item != Entity.Null && GetComponent<Identifieable>(item.Item).ItemId == requirement.ItemId;
                    if (!hasReq)
                    {
                        crafting.RemoveAt(0);
                        return;
                    }
                }
            }
            
            if (iBuffers.HasComponent(current.Recipe))
            {
                var hasRequirements = true;
                var inventory = GetBuffer<Inventory>(crafter.InputInventory);
                foreach (var requirement in iBuffers[current.Recipe])
                {
                    hasRequirements &=
                        inventory.Contains(identifiables, stackables, requirement.ItemId, requirement.Amount);
                }

                if (!hasRequirements)
                {
                    crafting.RemoveAt(0);
                    return;
                }
            }
            
            if (current.TimeLeft <= 0)
            {
                crafting.RemoveAt(0);
                var transaction = ItemTransactionBuilder
                    .Schedule(buffer, TransactionType.Remove, crafter.InputInventory)
                    .RequiresManualReview(new CraftingQueueReview { Crafter = e, IsRemoval = true, Data = current });
                foreach (var requirement in iBuffers[current.Recipe])
                {
                    transaction.With(requirement.ItemId, requirement.Amount);
                }
            }
        }).Schedule();

        Entities.ForEach((Entity e, in CraftingQueueReview review, in Completed result) =>
        {
            if (!review.IsRemoval && !result.Success)
                return;
            
            buffer.DestroyEntity(e);

            var current = review.Data;

            if (!review.IsRemoval || !result.Success) return;
            if (!oBuffers.HasComponent(current.Recipe)) return;
            
            var crafter = GetComponent<CraftingQueue>(review.Crafter);
            var transaction = ItemTransactionBuilder
                .Schedule(buffer, TransactionType.Add, crafter.OutputInventory)
                .RequiresManualReview(new CraftingQueueReview { Crafter = review.Crafter, IsRemoval = false });
            foreach (var requirement in oBuffers[current.Recipe])
            {
                transaction.With(requirement.ItemId, requirement.Amount);
            }
        }).Run();
    }

    struct CraftingQueueReview : ITransactionReviewData
    {
        public Entity Crafter;
        public bool IsRemoval;
        public CraftingBuffer Data;
    }
}
