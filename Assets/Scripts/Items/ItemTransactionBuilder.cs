using Unity.Entities;

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

public enum TransactionType
{
    Add,
    Remove
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