using Unity.Entities;

[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateBefore(typeof(InventoryInteractionSystem))]
public partial class PlayerInventorySystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<ControlScheme.UI.Inventory>();
    }

    protected override void OnStartRunning()
    {
        var localPlayer = SystemAPI.GetSingletonEntity<LocalPlayer>();
        if(!EntityManager.HasComponent<InventoryUISystem.Open>(localPlayer))
            EntityManager.AddComponentData(localPlayer, new InventoryUISystem.Open());
    }
    
    protected override void OnStopRunning()
    {
        EntityManager.RemoveComponent<InventoryUISystem.Open>(GetEntityQuery(typeof(InventoryUISystem.Open)));
    }

    protected override void OnUpdate() { }
}