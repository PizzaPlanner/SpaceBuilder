using Unity.Entities;

public partial class AdminScriptExecutionSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<IdentifiableMappingSystem.Loaded>();
    }

    protected override void OnUpdate()
    {
        var localPlayer = SystemAPI.GetSingletonEntity<LocalPlayer>();
        foreach (var (script, e) in SystemAPI.Query<ExecuteAdminScript>().WithEntityAccess())
        {
            AdminConsole.Execute(script.Script.Value, script.Executor == Entity.Null ? localPlayer : script.Executor);
        }
        EntityManager.DestroyEntity(GetEntityQuery(typeof(ExecuteAdminScript)));
    }
}