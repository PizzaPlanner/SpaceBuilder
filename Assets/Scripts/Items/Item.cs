using Unity.Entities;

public struct Item : IComponentData { }

public struct Stackable : IComponentData
{
    public ushort Size;
    public ushort MaxSize;
}

// TODO: Currently not used.
[UpdateBefore(typeof(InventoryManagementSystem))]
public partial class PickupItemSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // var inventories = GetBufferLookup<Inventory>(false);
        // var buffer = bufferSystem.CreateCommandBuffer();
        // Entities
        //     .WithAll<PhysicsWorldIndex>()
        //     .WithNone<DisableRendering>()
        //     .ForEach((Entity e, in Item item, in Interacted interaction) =>
        // {
        //     if (!inventories.HasComponent(interaction.Interactor))
        //         return;
        //
        //     var inv = inventories[interaction.Interactor];
        //     bool pickedUp = false;
        //     for(int i = 0; i < inv.Length; i++)
        //         if (inv[i].Slot.Item == Entity.Null)
        //         {
        //             var slot = inv[i];
        //             slot.Slot.Item = e;
        //             inv[i] = slot;
        //             pickedUp = true;
        //             break;
        //         }
        //
        //     if (pickedUp)
        //     {
        //         if (!HasComponent<Parent>(e))
        //         {
        //             buffer.AddComponent(e, new Parent{Value = interaction.Interactor});
        //             buffer.AddComponent<LocalToParent>(e);
        //         }
        //         SetComponent(e, new LocalToWorld{Value = float4x4.Translate(new float3(float.PositiveInfinity))});
        //         buffer.AddComponent<DisableRendering>(e);
        //         buffer.RemoveComponent<PhysicsWorldIndex>(e);
        //     }
        //
        // }).Run();
        //
        // bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
