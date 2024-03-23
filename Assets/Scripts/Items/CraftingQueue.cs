using System;
using Unity.Collections;
using Unity.Entities;

public readonly partial struct CrafterAspect : IAspect
{
    public readonly RefRO<Crafter> Crafter;
    public readonly DynamicBuffer<CraftingQueue> Queue;
    public readonly DynamicBuffer<CatalystInventory> Catalysts;
    public readonly DynamicBuffer<Craftables> Recipes;

    public bool HasCatalyst(Identifieable catalyst)
    {
        foreach(var item in Catalysts)
            if(item.ItemId == catalyst)
                return true;
        return false;
    }
}

public struct CraftingQueue : IBufferElementData
{
    public Entity Recipe;
    public float TimeLeft;
}

[UpdateAfter(typeof(InventoryManagementSystem))]
public partial class CraftingSystem : SystemBase
{
    partial struct CraftJob : IJobEntity
    {
        [ReadOnly] public ItemAspect.Lookup itemLookup;
        [ReadOnly] public BufferLookup<CraftingCatalyst> cBuffers;
        [ReadOnly] public BufferLookup<CraftingInput> iBuffers;
        [ReadOnly] public BufferLookup<Inventory> inventoryLookup;
        public EntityCommandBuffer ecb;
        public float deltaTime;

        void Execute(Entity e, CrafterAspect crafter)
        {
            if (crafter.Queue.Length == 0)
                return;

            var queue = crafter.Queue;
            var current = queue[0];
            current.TimeLeft -= deltaTime;
            queue[0] = current;

            if (cBuffers.HasBuffer(current.Recipe))
            {
                var cRecipe = cBuffers[current.Recipe];
            
                foreach (var requirement in cRecipe)
                {
                    if (!crafter.HasCatalyst(requirement.ItemId))
                    {
                        queue.RemoveAt(0);
                        return;
                    }
                }
            }
            
            if (iBuffers.HasBuffer(current.Recipe))
            {
                var hasRequirements = true;
                var inventory = inventoryLookup[crafter.Crafter.ValueRO.InputInventory];
                foreach (var requirement in iBuffers[current.Recipe])
                {
                    hasRequirements &=
                        inventory.Contains(itemLookup, requirement.ItemId, requirement.Amount);
                }

                if (!hasRequirements)
                {
                    queue.RemoveAt(0);
                    return;
                }
            }
            
            if (current.TimeLeft <= 0)
            {
                queue.RemoveAt(0);
                var transaction = ItemTransactionBuilder
                    .Schedule(ecb, TransactionType.Remove, crafter.Crafter.ValueRO.InputInventory)
                    .RequiresManualReview(new CraftingQueueReview { Crafter = e, IsRemoval = true, Data = current });
                foreach (var requirement in iBuffers[current.Recipe])
                {
                    transaction.With(requirement.ItemId, requirement.Amount);
                }
            }
        }
    }
    protected override void OnCreate()
    {
        RequireForUpdate<IdentifiableMappingSystem.Loaded>();
    }

    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(World.Unmanaged);
        var deltaTime = World.Time.DeltaTime;
        var cBuffers = GetBufferLookup<CraftingCatalyst>(true);
        var iBuffers = GetBufferLookup<CraftingInput>(true);
        var oBuffers = GetBufferLookup<CraftingOutput>(true);
        var itemLookup = new ItemAspect.Lookup(ref CheckedStateRef);
        var inventoryLookup = SystemAPI.GetBufferLookup<Inventory>(true);

        foreach(var (review, result, e) in 
                SystemAPI.Query<CraftingQueueReview, Completed>().WithEntityAccess())
        {
            if (!review.IsRemoval && !result.Success)
                return;
            
            ecb.DestroyEntity(e);

            var current = review.Data;

            if (!review.IsRemoval || !result.Success) return;
            if (!oBuffers.HasBuffer(current.Recipe)) return;
            
            var crafter = SystemAPI.GetComponent<Crafter>(review.Crafter);
            var transaction = ItemTransactionBuilder
                              .Schedule(ecb, TransactionType.Add, crafter.OutputInventory)
                              .RequiresManualReview(new CraftingQueueReview { Crafter = review.Crafter, IsRemoval = false });
            foreach (var requirement in oBuffers[current.Recipe])
            {
                transaction.With(requirement.ItemId, requirement.Amount);
            }
        }
        
        Dependency = new CraftJob
        {
            ecb = ecb,
            cBuffers = cBuffers,
            iBuffers = iBuffers,
            deltaTime = deltaTime,
            inventoryLookup = inventoryLookup,
            itemLookup = itemLookup
        }.Schedule(Dependency);
    }

    struct CraftingQueueReview : ITransactionReviewData
    {
        public Entity Crafter;
        public bool IsRemoval;
        public CraftingQueue Data;
    }
}
