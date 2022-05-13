using System.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

public struct GetItemAction : IComponentData
{
    public Entity Inventory;
    public long ItemId;
    public ushort Quantity;
}

public struct RemoveItem : IComponentData
{
    public ushort Quantity;
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
            var inventory = GetBuffer<Inventory>(action.Inventory);
            for(int i = 0; i < inventory.Length; i++)
                if (inventory[i].Slot.Item == Entity.Null || (GetComponent<Identifieable>(inventory[i].Slot.Item).ItemId == action.ItemId && HasComponent<Stackable>(inventory[i].Slot.Item)))
                {
                    var slot = inventory[i].Slot;
                    if (inventory[i].Slot.Item != Entity.Null)
                    {
                        var stack = GetComponent<Stackable>(inventory[i].Slot.Item);
                        if (stack.Size + action.Quantity <= stack.MaxSize)
                        {
                            SetComponent(slot.Item, new Stackable{Size = (ushort)(stack.Size + action.Quantity), MaxSize = stack.MaxSize});
                            break;
                        }
                    }
                    else
                    {
                        var prefab = prefabMap[action.ItemId];
                        slot.Item = EntityManager.Instantiate(prefab);
                        if(HasComponent<Stackable>(slot.Item))
                            SetComponent(slot.Item, new Stackable{MaxSize = GetComponent<Stackable>(prefab).MaxSize, Size = action.Quantity});
                        EntityManager.AddComponentData(slot.Item, new Interacted{Interactor = action.Inventory});
                        break;
                    }
                }
        }).WithoutBurst().WithStructuralChanges().Run();

        Entities
            .WithNone<Stackable>()
            .ForEach((Entity e, in Item item, in RemoveItem remove) =>
            {
                if (HasComponent<Parent>(e) && GetComponent<Parent>(e).Value != Entity.Null)
                {
                    buffer.RemoveComponent<DisableRendering>(e);
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

        Entities
            .ForEach((Entity e, ref Stackable stack, in Item item, in RemoveItem remove) =>
            {
                if (remove.Quantity >= stack.Size)
                {
                    if (HasComponent<Parent>(e) && GetComponent<Parent>(e).Value != Entity.Null)
                    {
                        buffer.RemoveComponent<DisableRendering>(e);
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
                    stack.Size -= remove.Quantity;
                    buffer.RemoveComponent<RemoveItem>(e);
                }
            }).Run();

        EntityManager.CreateEntity(typeof(InventoryUISystem.InventoryChanged));

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
