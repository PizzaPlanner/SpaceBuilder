using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

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
    public bool IsPaid;
}

[UpdateAfter(typeof(InventoryManagementSystem))]
public partial class CraftingSystem : SystemBase
{
    private EntityCommandBufferSystem bufferSystem;

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        RequireSingletonForUpdate<IdentifiableMappingSystem.Loaded>();
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer();
        var deltaTime = Time.DeltaTime;
        var cBuffers = GetBufferFromEntity<CraftingCatalyst>(true);
        var iBuffers = GetBufferFromEntity<CraftingInput>(true);
        var oBuffers = GetBufferFromEntity<CraftingOutput>(true);
        var reviewQuery = GetEntityQuery(typeof(CraftingQueueReview), typeof(Completed));
        var reviewEntities = reviewQuery.ToEntityArray(Allocator.TempJob);
        var reviews = reviewQuery.ToComponentDataArray<CraftingQueueReview>(Allocator.TempJob);
        var reviewResults = reviewQuery.ToComponentDataArray<Completed>(Allocator.TempJob);
        
        Entities
            .WithDisposeOnCompletion(reviews)
            .WithDisposeOnCompletion(reviewResults)
            .WithDisposeOnCompletion(reviewEntities)
            .ForEach((Entity e, ref DynamicBuffer<CraftingBuffer> crafting, in CraftingQueue crafter) =>
        {
            if (crafting.Length == 0)
                return;

            var current = crafting[0];
            current.TimeLeft -= deltaTime;

            // if (cBuffers.HasComponent(current.Recipe))
            // {
            //     var cInventory = GetBuffer<Inventory>(crafter.CatalystInventory);
            //     var cRecipe = cBuffers[current.Recipe];
            //
            //     foreach (var requirement in cRecipe)
            //     {
            //         bool hasReq = false;
            //         foreach (var item in cInventory)
            //             hasReq |= item.Item != Entity.Null && GetComponent<Identifieable>(item.Item).ItemId == requirement.ItemId;
            //         if (!hasReq)
            //         {
            //             crafting.RemoveAt(0);
            //             return;
            //         }
            //     }
            // }
            
            if (iBuffers.HasComponent(current.Recipe) && !current.IsPaid)
            {
                var transaction = ItemTransactionBuilder
                    .Schedule(buffer, TransactionType.Remove, crafter.InputInventory)
                    .RequiresManualReview(new CraftingQueueReview { Crafter = e, IsRemoval = true });
                foreach (var requirement in iBuffers[current.Recipe])
                {
                    transaction.With(requirement.ItemId, requirement.Amount);
                }
                current.IsPaid = true;
            }
            
            crafting[0] = current;

            for (int i = 0; i < reviews.Length; i++)
            {
                if(reviews[i].Crafter != e)
                    continue;
                
                var isRemoval = reviews[i].IsRemoval;
                var result = reviewResults[i].Success;
                Debug.Log($"Crafter review ready with result {result} and removal {isRemoval}");
                if (isRemoval && !result)
                {
                    // Cancel crafting
                    crafting.RemoveAt(0);
                    buffer.DestroyEntity(reviewEntities[i]);
                }
                else if (isRemoval && current.TimeLeft <= 0)
                {
                    crafting.RemoveAt(0);
                    buffer.DestroyEntity(reviewEntities[i]);
                    if (oBuffers.HasComponent(current.Recipe))
                    {
                        var transaction = ItemTransactionBuilder
                            .Schedule(buffer, TransactionType.Add, crafter.InputInventory)
                            .RequiresManualReview(new CraftingQueueReview { Crafter = e, IsRemoval = false });
                        foreach (var requirement in oBuffers[current.Recipe])
                        {
                            transaction.With(requirement.ItemId, requirement.Amount);
                        }
                    }
                }
                else if (!isRemoval && result)
                {
                    crafting.RemoveAt(0);
                    buffer.DestroyEntity(reviewEntities[i]);
                }
            }
        }).Schedule();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }

    struct CraftingQueueReview : ITransactionReviewData
    {
        public Entity Crafter;
        public bool IsRemoval;
    }
}
