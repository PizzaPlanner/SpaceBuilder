using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public enum TransactionType
{
    Add,
    Remove
}

public struct ItemTransactionBuilder
{
    private EntityCommandBuffer ecb;
    private Entity entity;
    private bool isManuallyReviewed;

    public static ItemTransactionBuilder Schedule(EntityCommandBuffer ecb, TransactionType type,
        Entity targetInventory)
    {
        var tx = new InventoryManagementSystem.ItemTransaction { Type = type, TargetInventory = targetInventory };
        var txe = ecb.CreateEntity();
        ecb.AddComponent(txe, tx);
        
        ecb.AddBuffer<ModifyItem>(txe);
        return new ItemTransactionBuilder { ecb = ecb, entity = txe };
    }
    
    public ItemTransactionBuilder With(ulong itemId, uint quantity)
    {
        ecb.AppendToBuffer(entity, new ModifyItem{ Quantity = quantity, ItemId = itemId });
        return this;
    }

    public ItemTransactionBuilder RequiresManualReview<T>() where T : unmanaged, ITransactionReviewData
    {
        if(!isManuallyReviewed)
            ecb.AddComponent<RequiresManualReview>(entity);
        isManuallyReviewed = true;
        ecb.AddComponent<T>(entity);
        return this;
    }
    
    public ItemTransactionBuilder RequiresManualReview<T>(T data) where T : unmanaged, ITransactionReviewData
    {
        if(!isManuallyReviewed)
            ecb.AddComponent<RequiresManualReview>(entity);
        isManuallyReviewed = true;
        ecb.AddComponent(entity, data);
        return this;
    }
}

public interface ITransactionReviewData : IComponentData { }
public struct RequiresManualReview : IComponentData { }

public struct Completed : IComponentData
{
    public bool Success;
}

public struct ModifyItem : IBufferElementData
{
    public uint Quantity;
    public uint QuantityFilled;
    public ulong ItemId;

    public uint QuantityLeft => Quantity - QuantityFilled;
}

public partial class InventoryManagementSystem : SystemBase
{
    public struct ItemTransaction : IComponentData
    {
        public Entity TargetInventory;
        public TransactionType Type;
    }
    
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
        RequireForUpdate(GetEntityQuery(typeof(ItemTransaction), typeof(ModifyItem)));
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

        var inventoryBuffer = GetBufferFromEntity<Inventory>(false);
        var addedTransactionsThisFrame = new NativeList<Entity>(Allocator.TempJob);
        var stackables = GetComponentDataFromEntity<Stackable>(false);
        var identifiables = GetComponentDataFromEntity<Identifieable>(true);
        Entities
            .WithReadOnly(identifiables)
            .WithDisposeOnCompletion(addedTransactionsThisFrame)
            .WithNone<Completed>()
            .ForEach((Entity e, DynamicBuffer<ModifyItem> addBuffer, ref ItemTransaction transaction) =>
        {
            // Only do one transaction per target inventory at a time
            if (addedTransactionsThisFrame.Contains(transaction.TargetInventory))
                return;
            
            // Fail if the target is not an inventory
            if (!inventoryBuffer.HasComponent(transaction.TargetInventory))
            {
                return;
            }

            // Block the target inventory with this as the next pending transaction
            if (!HasComponent<PendingTransaction>(transaction.TargetInventory))
            {
                ecb.AddComponent(transaction.TargetInventory, new PendingTransaction { Transaction = e });
                return;
            }
            
            // Mark as waiting and stop if there is another transaction ongoing
            if (GetComponent<PendingTransaction>(transaction.TargetInventory).Transaction != e)
            {
                return;
            }
            
            var inventory = inventoryBuffer[transaction.TargetInventory];

            var success = true;
            for (int addIdx = 0; addIdx < addBuffer.Length && success; addIdx++)
            {
                var operation = addBuffer[addIdx];
                var prefab = prefabMap[operation.ItemId];
                // TODO: This needs reversion logic or verify success beforehand
                success &= transaction.Type switch
                {
                    TransactionType.Add => AddItemToInventory(ecb, ref operation, stackables, identifiables, inventory, transaction.TargetInventory, prefab),
                    TransactionType.Remove => RemoveItemFromInventory(ecb, ref operation, stackables, identifiables, inventory),
                };

                addBuffer[addIdx] = operation;
            }
            
            ecb.RemoveComponent<PendingTransaction>(transaction.TargetInventory);

            if (!HasComponent<RequiresManualReview>(e))
                ecb.DestroyEntity(e);
            else
                ecb.AddComponent(e, new Completed { Success = success });
            
            if(!success)
                Debug.LogError($"Item Transaction '{transaction.Type}' on {transaction.TargetInventory} failed");
        }).Schedule();

        Job.WithCode(() =>
        {
            var change = ecb.CreateEntity();
            ecb.AddComponent(change, new InventoryUISystem.InventoryChanged());
        }).Schedule();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }

    static bool AddItemToInventory(EntityCommandBuffer ecb, ref ModifyItem operation, 
        ComponentDataFromEntity<Stackable> stackables, 
        ComponentDataFromEntity<Identifieable> identifiables, 
        DynamicBuffer<Inventory> inventory,
        Entity inventoryEntity,
        Entity itemPrefab)
    {
        // Check where the new item would fit
        for (int i = 0; i < inventory.Length && operation.QuantityLeft > 0; i++)
        {
            if (inventory[i].Item != Entity.Null &&
                (identifiables[inventory[i].Item].ItemId != operation.ItemId ||
                 !stackables.HasComponent(inventory[i].Item)))
                // Item can not fit in this slot
                continue;

            var slot = inventory[i];
            if (slot.Item != Entity.Null)
            {
                var stack = stackables[slot.Item];
                var room = (ushort)(stack.MaxSize - stack.Size);
                var toAdd = (ushort)math.min(room, operation.QuantityLeft);
                stack.Size += toAdd;
                stackables[slot.Item] = stack;
                operation.QuantityFilled += toAdd;
            }
            else
            {
                // Empty slot, make new item
                var newItem = ecb.Instantiate(itemPrefab);
                ecb.AddComponent(newItem, new RegisterWithInventory { Inventory = inventoryEntity, Slot = i });
                if (stackables.HasComponent(itemPrefab))
                {
                    var maxStack = stackables[itemPrefab].MaxSize;
                    var currStack = (ushort)math.min(maxStack, operation.QuantityLeft);
                    ecb.SetComponent(newItem, new Stackable { MaxSize = maxStack, Size = currStack });
                    operation.QuantityFilled += currStack;
                }
                else
                    operation.QuantityFilled += 1;
            }
        }

        return operation.QuantityLeft == 0;
    }

    static bool RemoveItemFromInventory(EntityCommandBuffer ecb, ref ModifyItem operation,
        ComponentDataFromEntity<Stackable> stackables,
        ComponentDataFromEntity<Identifieable> identifiables,
        DynamicBuffer<Inventory> inventory)
    {
        for (int i = 0; i < inventory.Length && operation.QuantityLeft > 0; i++)
        {
            if (inventory[i].Item == Entity.Null || identifiables[inventory[i].Item].ItemId != operation.ItemId)
                // Can not remove from this slot
                continue;

            var slot = inventory[i];
            var isSlotEmpty = true;
            if (stackables.HasComponent(slot.Item))
            {
                var stack = stackables[slot.Item];
                var toRemove = (ushort)math.min(stack.Size, operation.QuantityLeft);
                stack.Size -= toRemove;
                isSlotEmpty = stack.Size == 0;
                stackables[slot.Item] = stack;
                operation.QuantityFilled += toRemove;
            }
            else
                operation.QuantityFilled += 1;

            if (!isSlotEmpty) continue;
            ecb.DestroyEntity(slot.Item);
            slot.Item = Entity.Null;
            inventory[i] = slot;
        }
        
        return operation.QuantityLeft == 0;
    }
}
