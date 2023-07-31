using Unity.Entities;

[UpdateInGroup(typeof(InputSystemGroup), OrderLast = true)]
partial class UIInputSystem : SystemBase
{
    protected override void OnCreate()
    {
        var query = SystemAPI.QueryBuilder().WithAny<
            ControlScheme.UI.BuildingParts, 
            ControlScheme.UI.MainMenu, 
            ControlScheme.UI.TabMenu,
            ControlScheme.UI.Inventory,
            ControlScheme.UI.LogicDisplay>().Build();
        RequireForUpdate(query);
    }

    protected override void OnStartRunning()
    {
        EntityManager.CreateEntity(typeof(ControlScheme.UI));
    }

    protected override void OnStopRunning()
    {
        if(SystemAPI.TryGetSingletonEntity<ControlScheme.UI>(out var e))
            EntityManager.DestroyEntity(e);
    }

    protected override void OnUpdate() { }
}