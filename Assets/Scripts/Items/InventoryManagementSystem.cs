using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct RemoveItem : IBufferElementData
{
    public uint Quantity;
    public ulong ItemId;
    public Entity MoveToTargetInventory;
}

public struct AddItem : IBufferElementData
{
    public uint Quantity;
    public ulong ItemId;
}

public partial class InventoryManagementSystem : SystemBase
{
    // Workaround for not getting the correct entity when using an ECB
    private struct RegisterWithInventory : IComponentData
    {
        public Entity Inventory;
        public int Slot;
    }
    
    private EndSimulationEntityCommandBufferSystem bufferSystem;

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        RequireSingletonForUpdate<IdentifiableMappingSystem.Loaded>();
        RequireForUpdate(GetEntityQuery(new EntityQueryDesc
        {
            Any = new[] { ComponentType.ReadWrite<AddItem>(), ComponentType.ReadWrite<RemoveItem>() }, 
            All = new[] { ComponentType.ReadWrite<Inventory>() }
        }));
    }

    protected override void OnUpdate()
    {
        var ecb = bufferSystem.CreateCommandBuffer();
        var prefabMap = IdentifiableMappingSystem.EntitiesById;
        
        Entities.ForEach((Entity e, in RegisterWithInventory rwi) =>
        {
            var inventory = GetBuffer<Inventory>(rwi.Inventory);
            var slot = inventory[rwi.Slot]; 
            slot.Item = e;
            inventory[rwi.Slot] = slot;
            
            ecb.RemoveComponent<RegisterWithInventory>(e);
        }).Run();
        
        Entities.ForEach((Entity e, DynamicBuffer<AddItem> addBuffer, DynamicBuffer<Inventory> inventory) =>
        {
            for (int addIdx = 0; addIdx < addBuffer.Length; addIdx++)
            {
                var quantityLeft = addBuffer[addIdx].Quantity;
                var idToAdd = addBuffer[addIdx].ItemId;

                // Check where the new item would fit
                for (int i = 0; i < inventory.Length && quantityLeft > 0; i++)
                {

                    if (inventory[i].Item != Entity.Null &&
                        (GetComponent<Identifieable>(inventory[i].Item).ItemId != idToAdd ||
                         !HasComponent<Stackable>(inventory[i].Item)))
                        // Item can not fit in this slot
                        continue;

                    var slot = inventory[i];
                    if (slot.Item != Entity.Null)
                    {
                        var stack = GetComponent<Stackable>(slot.Item);
                        var room = (ushort)(stack.MaxSize - stack.Size);
                        var toAdd = (ushort)math.min(room, quantityLeft);
                        stack.Size += toAdd;
                        SetComponent(slot.Item, stack);
                        quantityLeft -= toAdd;
                    }
                    else
                    {
                        // Empty slot, make new item
                        var prefab = prefabMap[idToAdd];
                        var newItem = ecb.Instantiate(prefab);
                        ecb.AddComponent(newItem, new RegisterWithInventory{Inventory = e, Slot = i});
                        if (HasComponent<Stackable>(prefab))
                        {
                            var maxStack = GetComponent<Stackable>(prefab).MaxSize;
                            var currStack = (ushort)math.min(maxStack, quantityLeft);
                            ecb.SetComponent(newItem, new Stackable { MaxSize = maxStack, Size = currStack });
                            quantityLeft -= currStack;
                        }
                        else
                            quantityLeft -= 1;
                        
                    }
                }

                var addOp = addBuffer[addIdx];
                addOp.Quantity = quantityLeft;
                addBuffer[addIdx] = addOp;
            }
            
            for(int i = addBuffer.Length - 1; i >= 0; i--)
                if(addBuffer[i].Quantity == 0)
                    addBuffer.RemoveAt(i);
        }).Run();

        Entities.ForEach((Entity e, DynamicBuffer<RemoveItem> removeBuffer, DynamicBuffer<Inventory> inventory) =>
        {
            for (int addIdx = 0; addIdx < removeBuffer.Length; addIdx++)
            {
                var quantityLeft = removeBuffer[addIdx].Quantity;
                var idToRemove = removeBuffer[addIdx].ItemId;

                for (int i = 0; i < inventory.Length && quantityLeft > 0; i++)
                {
                    if (inventory[i].Item == Entity.Null ||
                        GetComponent<Identifieable>(inventory[i].Item).ItemId != idToRemove)
                        // Can not remove from this slot
                        continue;

                    var slot = inventory[i];
                    var toRemove = (ushort)1;
                    var isSlotEmpty = true;
                    if (HasComponent<Stackable>(slot.Item))
                    {
                        var stack = GetComponent<Stackable>(inventory[i].Item);
                        toRemove = (ushort)math.min(stack.Size, quantityLeft);
                        stack.Size -= toRemove;
                        isSlotEmpty = stack.Size == 0;
                        SetComponent(slot.Item, stack);
                        quantityLeft -= toRemove;
                    }
                    else
                        quantityLeft -= 1;

                    // Add removed items to other inventory
                    if (removeBuffer[addIdx].MoveToTargetInventory != Entity.Null)
                        ecb.AppendToBuffer(removeBuffer[addIdx].MoveToTargetInventory,
                            new AddItem { ItemId = idToRemove, Quantity = toRemove });

                    if(isSlotEmpty)
                    {
                        ecb.DestroyEntity(slot.Item);
                        slot.Item = Entity.Null;
                        inventory[i] = slot;
                    }
                }
                
                var addOp = removeBuffer[addIdx];
                addOp.Quantity = quantityLeft;
                removeBuffer[addIdx] = addOp;
            }
            
            for(int i = removeBuffer.Length - 1; i >= 0; i--)
                if(removeBuffer[i].Quantity == 0)
                    removeBuffer.RemoveAt(i);
        }).Run();

        EntityManager.CreateEntity(typeof(InventoryUISystem.InventoryChanged));

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
