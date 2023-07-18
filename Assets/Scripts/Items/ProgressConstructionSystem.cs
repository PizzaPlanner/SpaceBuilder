using Unity.Entities;
using Unity.Mathematics;

namespace Parts
{
    public partial class ProgressConstructionSystem : SystemBase
    {
        struct ConstructorReview : ITransactionReviewData
        {
            public Entity Target;
            public Entity Constructor;
            public double ConstructorLastTick;
            public uint MaxProgress;
        }

        protected override void OnUpdate()
        {
            var ecb = GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            var time = World.Time.ElapsedTime;

            Entities.ForEach((Entity e, in ConstructorReview review, in Completed result) =>
            {
                ecb.DestroyEntity(e);

                var printer = GetComponent<Constructor>(review.Constructor);
                printer.LastTick = review.ConstructorLastTick;
                printer.TimeSpentOnItem += (float)(time - printer.LastTick);
                SetComponent(review.Constructor, printer);
                
                if (!result.Success)
                    return;

                var refundItems = !HasComponent<UnderConstruction>(review.Target);
                if (!refundItems)
                {
                    var current = GetComponent<UnderConstruction>(review.Target);
                    current.Progress++;
                    SetComponent(review.Target, current);

                    if(current.Progress == review.MaxProgress)
                    {
                        // Finish construction
                        ecb.AddComponent<FinishConstruction>(review.Target);
                        printer.LastTick = time + printer.DelayPerBuilding;
                        printer.TimeSpentOnItem = 0;
                        SetComponent(review.Constructor, printer);
                    } 
                    else
                        refundItems = current.Progress > review.MaxProgress;
                }

                if (!refundItems) return;
            
                // Give items back, since part was already fully constructed
                var tx = ItemTransactionBuilder.Schedule(ecb, TransactionType.Add, review.Constructor);
                foreach(var item in GetBuffer<ModifyItem>(e))
                    tx.With(item.ItemId, item.QuantityFilled);
            }).Run();
        
            var constructionBuffers = GetBufferLookup<ConstructionInputBuffer>(true);
            var underConstructions = GetComponentLookup<UnderConstruction>(true);
            var gridItems = GetComponentLookup<GridItem>(true);
            var identifieables = GetComponentLookup<Identifieable>(true);
        
            Entities
                .WithReadOnly(underConstructions)
                .WithReadOnly(gridItems)
                .WithReadOnly(identifieables)
                .WithNone<PendingTransaction>()
                .ForEach((Entity e, ref Constructor printer, in DynamicBuffer<Inventory> inventory, in Target target) =>
                {
                    if (!target.CanConstruct)
                        return;
                
                    if (!constructionBuffers.HasComponent(target.Value))
                        return;

                    if (target.Value != target.PreviousValue)
                    {
                        printer.LastTick = time + printer.DelayPerBuilding;
                        printer.TimeSpentOnItem = 0;
                    }

                    var parts = constructionBuffers[target.Value];
                    var current = underConstructions[target.Value];
                    var partScale = 1u;
                    if (gridItems.TryGetComponent(target.Value, out var item) && item.Scalable)
                    {
                        var size = item.Area.Size;
                        partScale = (uint)(size.x * size.y * size.z);
                    }
                    var maxProgress = 0u;
                    var progress = current.Progress;

                    var itemIdToConsume = 0ul;
                    for (int i = 0; i < parts.Length && progress < uint.MaxValue; i++)
                    {
                        var partCost = parts[i].Amount * partScale;
            
                        maxProgress += partCost;
                        if (progress >= partCost)
                        {
                            progress -= partCost;
                            if(progress == 0)
                                printer.TimeSpentOnItem = 0;
                        }
                        else
                        {
                            if (inventory.TryFindItem(identifieables, parts[i].ItemId, out _))
                                itemIdToConsume = parts[i].ItemId;
                            progress = uint.MaxValue;
                        }
                    }

                    if (!(time - printer.LastTick > math.lerp(printer.InitialDelayPerItem, printer.MinDelayPerItem,
                            printer.TimeSpentOnItem / 5))) return;
                    if (itemIdToConsume == 0ul) return;
                
                    ItemTransactionBuilder.Schedule(ecb, TransactionType.Remove, e)
                                          .With(itemIdToConsume, 1)
                                          .RequiresManualReview(new ConstructorReview
                                          {
                                              Constructor = e,
                                              Target = target.Value,
                                              ConstructorLastTick = printer.LastTick,
                                              MaxProgress = maxProgress
                                          });
                    printer.LastTick = double.PositiveInfinity;
                }).Schedule();
        }
    }
}