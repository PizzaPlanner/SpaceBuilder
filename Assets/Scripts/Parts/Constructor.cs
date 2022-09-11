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

public partial struct ConstructionJob : IJobEntity
{
    public EntityCommandBuffer ecb;
    [ReadOnly] public BufferFromEntity<ConstructionInputBuffer> constructionBuffers;
    [ReadOnly] public ComponentDataFromEntity<Identifieable> identifieables;
    [ReadOnly] public ComponentDataFromEntity<GridItem> gridItems;
    public ComponentDataFromEntity<UnderConstruction> underConstructions;
    public double time;
    public void Execute(ref Constructor printer, in DynamicBuffer<Inventory> inventory, in Target target, DynamicBuffer<RemoveItem> removeItems)
    {
        if (!target.CanConstruct)
            return;

        if (!constructionBuffers.HasComponent(target.Value))
            return;

        if (removeItems.Length > 0)
            return;

        if (target.Value != target.PreviousValue)
        {
            printer.LastTick = time + printer.DelayPerBuilding;
            printer.TimeSpentOnItem = 0;
        }

        var parts = constructionBuffers[target.Value];
        var current = underConstructions[target.Value];
        var partScale = 1u;
        if (gridItems.HasComponent(target.Value) && gridItems[target.Value].Scalable)
        {
            var size = gridItems[target.Value].Area.Size;
            partScale = (uint)(size.x * size.y * size.z);
        }
        var maxProgress = 0u;
        var progress = current.Progress;

        var itemToConsume = Entity.Null;
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
                for(int j = 0; j < inventory.Length; j++)
                    if (inventory[j].Item != Entity.Null && identifieables[inventory[j].Item].ItemId == parts[i].ItemId)
                    {
                        itemToConsume = inventory[j].Item;
                        break;
                    }
                progress = uint.MaxValue;
            }
        }

        if (current.Progress >= maxProgress)
        {
            ecb.AddComponent<FinishConstruction>(target.Value);
            printer.LastTick = time + printer.DelayPerBuilding;
            printer.TimeSpentOnItem = 0;
            return;
        }
        
        if (time - printer.LastTick > math.lerp(printer.InitialDelayPerItem, printer.MinDelayPerItem, printer.TimeSpentOnItem / 5))
        {
            if (itemToConsume != Entity.Null)
            {
                // TODO: This causes a lot of issues if multiple constructors work on the same item!
                // If the item for the request doesnt exist another constructor can still manually finish construction, effectively cheating an item
                removeItems.Add(new RemoveItem { Quantity = 1, ItemId = identifieables[itemToConsume].ItemId });
                current.Progress++;
                
                underConstructions[target.Value] = current;
                printer.TimeSpentOnItem += (float)(time - printer.LastTick);
                printer.LastTick = time;
            }
        }
    }
}

public partial class ProgressConstructionSystem : SystemBase
{
    private EntityCommandBufferSystem bufferSystem;
    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        new ConstructionJob
        {
            constructionBuffers = GetBufferFromEntity<ConstructionInputBuffer>(true),
            identifieables = GetComponentDataFromEntity<Identifieable>(true),
            gridItems = GetComponentDataFromEntity<GridItem>(true),
            underConstructions = GetComponentDataFromEntity<UnderConstruction>(),
            time = Time.ElapsedTime,
            ecb = bufferSystem.CreateCommandBuffer()
        }.Schedule();

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
