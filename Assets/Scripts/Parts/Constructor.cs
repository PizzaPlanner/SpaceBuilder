using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[GenerateAuthoringComponent]
public struct Constructor : IComponentData
{
    public float DelayPerItem;
    public float DelayPerBuilding;
    public double LastTick;
}

public partial struct ConstructionJob : IJobEntity
{
    public EntityCommandBuffer ecb;
    [ReadOnly] public BufferFromEntity<ConstructionInputBuffer> constructionBuffers;
    [ReadOnly] public ComponentDataFromEntity<Identifieable> identifieables;
    public ComponentDataFromEntity<UnderConstruction> underConstructions;
    public double time;
    public void Execute(ref Constructor printer, in DynamicBuffer<Inventory> inventory, in Target target)
    {
        if (!target.CanConstruct)
            return;

        if (!constructionBuffers.HasComponent(target.Value))
            return;

        var parts = constructionBuffers[target.Value];
        var current = underConstructions[target.Value];
        var maxProgress = 0;
        var progress = current.Progress;

        var itemToConsume = Entity.Null;
        for (int i = 0; i < parts.Length && progress < ushort.MaxValue; i++)
        {
            maxProgress += parts[i].Amount;
            if (progress >= parts[i].Amount)
            {
                progress -= parts[i].Amount;
            }
            else
            {
                for(int j = 0; j < inventory.Length; j++)
                    if (inventory[j].Slot.Item != Entity.Null && identifieables[inventory[j].Slot.Item].ItemId == parts[i].ItemId)
                    {
                        itemToConsume = inventory[j].Slot.Item;
                        break;
                    }
                progress = ushort.MaxValue;
            }
        }

        if (time - printer.LastTick > printer.DelayPerItem && itemToConsume != Entity.Null)
        {
            ecb.AddComponent(itemToConsume, new RemoveItem {Quantity = 1});
            current.Progress++;
            if (current.Progress >= maxProgress)
            {
                ecb.AddComponent<FinishConstruction>(target.Value);
                printer.LastTick = time + printer.DelayPerBuilding;
            }
            else
            {
                underConstructions[target.Value] = current;
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
            occupations = GetBufferFromEntity<OccupationBuffer>(true),
            prefabMap = IdentifiableMappingSystem.EntitiesById,
            IsFinishConstructionJob = true
        }.Run(ppquery);
        EntityManager.DestroyEntity(pp);
        EntityManager.AddComponent<Disabled>(bp);
    }
}
