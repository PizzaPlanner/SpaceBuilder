using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

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
                var iInventory = GetBuffer<Inventory>(crafter.InputInventory);
                var iRemoves = GetBuffer<RemoveItem>(crafter.InputInventory);
                var iRecipe = iBuffers[current.Recipe];

                foreach (var requirement in iRecipe)
                {
                    var hasAmount = 0;
                    foreach (var item in iInventory)
                    {
                        if (hasAmount >= requirement.Amount ||
                            item.Item == Entity.Null ||
                            GetComponent<Identifieable>(item.Item).ItemId != requirement.ItemId)
                            continue;

                        var iEntity = item.Item;
                        var stackSize = HasComponent<Stackable>(iEntity) ? GetComponent<Stackable>(iEntity).Size : (ushort)1;

                        if (stackSize > 0 && current.TimeLeft <= 0)
                            iRemoves.Add(new RemoveItem
                            {
                                Quantity = (ushort)math.min(requirement.Amount - hasAmount, stackSize),
                                ItemId = requirement.ItemId
                            });

                        hasAmount += stackSize;
                    }

                    if (hasAmount < requirement.Amount)
                    {
                        crafting.RemoveAt(0);
                        return;
                    }
                }
            }

            if (current.TimeLeft <= 0 && (!iBuffers.HasComponent(current.Recipe) || GetBuffer<RemoveItem>(crafter.InputInventory).Length == 0))
            {
                var addBuffer = GetBuffer<AddItem>(crafter.OutputInventory);
                foreach (var output in GetBuffer<CraftingOutput>(current.Recipe))
                    addBuffer.Add(new AddItem { Quantity = output.Amount, ItemId = output.ItemId });

                crafting.RemoveAt(0);
            }
        }).Schedule();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
