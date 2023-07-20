using Unity.Entities;

public struct Inventory : IBufferElementData
{
    public Entity Item;
}


public struct PendingTransaction : IComponentData
{
    public Entity Transaction;
}

public static class InventoryHelper
{
    public static bool Contains(this DynamicBuffer<Inventory> buffer, ComponentLookup<Identifieable> idBuffer, ComponentLookup<Stackable> stackBuffer, ulong itemId, ushort amount)
    {
        ushort has = 0;
        for(int i = 0; i < buffer.Length; i++)
            if (buffer[i].Item != Entity.Null && idBuffer[buffer[i].Item].ItemId == itemId)
            {
                var item = buffer[i].Item;
                has += stackBuffer.HasComponent(item) ? stackBuffer[item].Size : (ushort)1;
            }

        return has >= amount;
    }
    
    public static bool TryFindItem(this DynamicBuffer<Inventory> inventory, ComponentLookup<Identifieable> idBuffer, ulong itemId, out int slot)
    {
        slot = -1;
        for(int j = 0; j < inventory.Length; j++)
            if (inventory[j].Item != Entity.Null && idBuffer.TryGetComponent(inventory[j].Item, out var item) && item.ItemId == itemId)
            {
                slot = j;
                return true;
            }

        return false;
    }
}