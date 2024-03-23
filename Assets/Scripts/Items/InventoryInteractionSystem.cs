using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class InventoryInteractionSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<Interacted>();
    }

    protected override void OnUpdate()
    {
        if(!SystemAPI.TryGetSingletonEntity<LocalPlayer>(out var localPlayer))
            return;
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (interacted, inventory, e) in
                 SystemAPI.Query<Interacted, DynamicBuffer<Inventory>>()
                          .WithEntityAccess())
        {
            Debug.Log($"Interacted with {e}");
            if(interacted.Interactor != localPlayer)
                return;

            if (!EntityManager.HasComponent<InventoryUISystem.Open>(e))
            {
                ecb.AddComponent<InventoryUISystem.Open>(e);
            }
            else
            {
                ecb.RemoveComponent<InventoryUISystem.Open>(e);
            }
        }
        ecb.Playback(EntityManager);
    }
}