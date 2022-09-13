using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[GenerateAuthoringComponent]
public struct Constructor : IComponentData
{
    public float DelayPerBuilding;
    public float InitialDelayPerItem;
    public float MinDelayPerItem;
    public float TimeSpentOnItem;
    public double LastTick;
}

public partial class ProgressConstructionSystem : SystemBase
{
    struct ConstructorReview : ITransactionReviewData
    {
        public Entity Target;
        public Entity Constructor;
        public double ConstructorLastTick;
        public uint MaxProgress;
    }

    private EntityCommandBufferSystem bufferSystem;
    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var ecb = bufferSystem.CreateCommandBuffer();
        var time = Time.ElapsedTime;

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
        
        var constructionBuffers = GetBufferFromEntity<ConstructionInputBuffer>(true);
        var underConstructions = GetComponentDataFromEntity<UnderConstruction>(true);
        var gridItems = GetComponentDataFromEntity<GridItem>(true);
        var identifieables = GetComponentDataFromEntity<Identifieable>(true);
        
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

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}

[UpdateBefore(typeof(ProgressConstructionSystem))]
public partial class FinishConstructionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var entity in GetEntityQuery(typeof(Disabled), typeof(FinishConstruction)).ToEntityArray(Allocator.Temp))
            EntityManager.DestroyEntity(entity);
        Entities.WithAll<FinishConstruction, UnderConstruction>().ForEach((Entity e) =>
        {
            ConstructBlueprint(e);
        }).WithStructuralChanges().WithoutBurst().Run();
    }

    private void ConstructBlueprint(Entity bp)
    {
        var pp = EntityManager.CreateEntity(typeof(PendingPart), typeof(UnderConstruction));
        var rotation = GetComponent<Rotation>(bp).Value;
        SetComponent(pp, new PendingPart
        {
            Data = new BuildingPartInfo
            {
                Id = GetComponent<Identifieable>(bp).ItemId,
                Area = GetComponent<GridItem>(bp).Area,
                Orientation = new RightAngleRotation(rotation),
                BuildStatus = ushort.MaxValue
            },
            Grid = GetComponent<GridItem>(bp).Grid
        });
        var ppquery = GetEntityQuery(typeof(UnderConstruction), typeof(PendingPart));
        new BuildingSystem.BuildJob
        {
            buffer = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>().CreateCommandBuffer(),
            grids = GetComponentDataFromEntity<Grid>(true),
            gridItems = GetComponentDataFromEntity<GridItem>(true),
            colliders = GetComponentDataFromEntity<PhysicsCollider>(true),
            occupations = GetBufferFromEntity<OccupationBuffer>(true),
            prefabMap = IdentifiableMappingSystem.EntitiesById,
            IsFinishConstructionJob = true
        }.Run(ppquery);
        EntityManager.DestroyEntity(pp);
        EntityManager.AddComponent<Disabled>(bp);

        EntityManager.CreateEntity(typeof(GridNeedsUpdate));
    }
}
