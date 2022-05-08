using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct CraftingQueue : IComponentData
{
    public Entity InputInventory;
    public Entity OutputInventory;
    public Entity CatalystInventory;
}

public struct CraftingBuffer : IBufferElementData
{
    public Entity Recipe;
    public float TimeLeft;
}

[UpdateAfter(typeof(InventoryManagementSystem))]
public partial class CraftingSystem : SystemBase
{
    private EntityCommandBufferSystem bufferSystem;

    struct RemoveFromEntity
    {
        public Entity Entity;
        public ushort Amount;
    }

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

        Entities
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
                        hasReq |= item.Slot.Item != Entity.Null && GetComponent<Identifieable>(item.Slot.Item).ItemId == requirement.ItemId;
                    if (!hasReq)
                    {
                        crafting.RemoveAt(0);
                        return;
                    }
                }
            }

            var itemsToRemoveFrom = new NativeList<RemoveFromEntity>(Allocator.Temp);
            if (iBuffers.HasComponent(current.Recipe))
            {
                var iInventory = GetBuffer<Inventory>(crafter.InputInventory);
                var iRecipe = iBuffers[current.Recipe];

                foreach (var requirement in iRecipe)
                {
                    var hasAmount = 0;
                    foreach (var item in iInventory)
                    {
                        if (hasAmount >= requirement.Amount ||
                            item.Slot.Item == Entity.Null ||
                            GetComponent<Identifieable>(item.Slot.Item).ItemId != requirement.ItemId)
                            continue;

                        var iEntity = item.Slot.Item;
                        var stackSize = HasComponent<Stackable>(iEntity) ? GetComponent<Stackable>(iEntity).Size : (ushort)1;
                        if (HasComponent<RemoveItem>(iEntity))
                            stackSize -= GetComponent<RemoveItem>(iEntity).Quantity;

                        if(stackSize > 0 && current.TimeLeft <= 0)
                            itemsToRemoveFrom.Add(new RemoveFromEntity{
                                Amount = (ushort)math.min(requirement.Amount - hasAmount, stackSize),
                                Entity = iEntity});

                        hasAmount += stackSize;
                    }

                    if (hasAmount < requirement.Amount)
                    {
                        crafting.RemoveAt(0);
                        return;
                    }
                }
            }

            if (current.TimeLeft <= 0)
            {
                foreach (var remove in itemsToRemoveFrom)
                    buffer.AddComponent(remove.Entity, new RemoveItem{Quantity = remove.Amount});

                foreach (var output in GetBuffer<CraftingOutput>(current.Recipe))
                {
                    var add = buffer.CreateEntity();
                    buffer.AddComponent(add, new GetItemAction{Inventory = crafter.OutputInventory, Quantity = output.Amount, ItemId = output.ItemId});
                }

                crafting.RemoveAt(0);
            }
        }).Schedule();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
