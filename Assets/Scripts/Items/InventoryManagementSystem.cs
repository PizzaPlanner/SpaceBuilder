using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct GetItemAction : IComponentData
{
    public Entity Inventory;
    public ulong ItemId;
    public uint Quantity;
}

public struct RemoveItem : IComponentData
{
    public uint Quantity;
}

public partial class InventoryManagementSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        RequireSingletonForUpdate<IdentifiableMappingSystem.Loaded>();
        RequireForUpdate(GetEntityQuery(new EntityQueryDesc{Any = new[]{ComponentType.ReadWrite<GetItemAction>(), ComponentType.ReadWrite<RemoveItem>()}}));
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer();
        var prefabMap = IdentifiableMappingSystem.EntitiesById;
        Entities.ForEach((Entity e, in GetItemAction action) =>
        {
            buffer.DestroyEntity(e);
            var quantityLeft = action.Quantity;
            var inventory = GetBuffer<Inventory>(action.Inventory, false);
            for(int i = 0; i < inventory.Length && quantityLeft > 0; i++)
                if (inventory[i].Slot.Item == Entity.Null || (GetComponent<Identifieable>(inventory[i].Slot.Item).ItemId == action.ItemId && HasComponent<Stackable>(inventory[i].Slot.Item)))
                {
                    var slot = inventory[i].Slot;
                    if (slot.Item != Entity.Null)
                    {
                        var stack = GetComponent<Stackable>(inventory[i].Slot.Item);
                        if (stack.Size + quantityLeft <= stack.MaxSize)
                        {
                            SetComponent(slot.Item, new Stackable{Size = (ushort)(stack.Size + quantityLeft), MaxSize = stack.MaxSize});
                            quantityLeft = 0;
                        }
                        else
                        {
                            quantityLeft = (uint)(stack.MaxSize - stack.Size);
                            SetComponent(slot.Item, new Stackable{Size = stack.MaxSize, MaxSize = stack.MaxSize});
                        }
                    }
                    else
                    {
                        var prefab = prefabMap[action.ItemId];
                        slot.Item = EntityManager.Instantiate(prefab);
                        if (!HasComponent<Parent>(slot.Item))
                        {
                            EntityManager.AddComponentData(slot.Item , new Parent{Value = action.Inventory});
                            EntityManager.AddComponent<LocalToParent>(e);
                        }

                        if (HasComponent<Stackable>(slot.Item))
                        {
                            var maxStack = GetComponent<Stackable>(prefab).MaxSize;
                            var currStack = (ushort)math.min(maxStack, quantityLeft);
                            SetComponent(slot.Item, new Stackable { MaxSize = maxStack, Size = currStack });
                            quantityLeft -= currStack;
                        }
                        else
                            quantityLeft -= 1;
                        EntityManager.AddComponentData(slot.Item, new Interacted{Interactor = action.Inventory});
                        inventory[i] = new Inventory {Slot = slot};
                    }
                }
        }).WithoutBurst().WithStructuralChanges().Run();

        Entities
            .WithNone<Stackable>()
            .ForEach((Entity e, in Item item, in RemoveItem remove) =>
            {
                if (HasComponent<Parent>(e) && GetComponent<Parent>(e).Value != Entity.Null)
                {
                    var parent = GetComponent<Parent>(e).Value;
                    var inv = GetBuffer<Inventory>(parent);
                    for(int i = 0; i < inv.Length; i++)
                        if (inv[i].Slot.Item == e)
                        {
                            var slot = inv[i].Slot;
                            slot.Item = Entity.Null;
                            inv[i] = new Inventory {Slot = slot};
                        }
                }
                buffer.DestroyEntity(e);
            }).Run();

        // TODO: This is wrong
        Entities
            .ForEach((Entity e, ref Stackable stack, in Item item, in RemoveItem remove) =>
            {
                var quantityLeft = remove.Quantity;
                if (quantityLeft >= stack.Size)
                {
                    if (HasComponent<Parent>(e) && GetComponent<Parent>(e).Value != Entity.Null)
                    {
                        var parent = GetComponent<Parent>(e).Value;
                        var inv = GetBuffer<Inventory>(parent);
                        for(int i = 0; i < inv.Length; i++)
                            if (inv[i].Slot.Item == e)
                            {
                                var slot = inv[i].Slot;
                                slot.Item = Entity.Null;
                                inv[i] = new Inventory {Slot = slot};
                            }
                    }
                    buffer.DestroyEntity(e);
                }
                else
                {
                    stack.Size -= (ushort)remove.Quantity;
                    buffer.RemoveComponent<RemoveItem>(e);
                }
            }).Run();

        EntityManager.CreateEntity(typeof(InventoryUISystem.InventoryChanged));

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
