using Unity.Entities;

public partial class SteeringToggleSystem : SystemBase
{
    private InputSystem inputSystem;

    protected override void OnCreate()
    {
        inputSystem = World.GetExistingSystemManaged<InputSystem>();
        RequireForUpdate<LocalPlayer>();
    }

    protected override void OnUpdate()
    {
        var localPlayer = GetSingletonEntity<LocalPlayer>();
        var input = GetSingleton<InputSystem.InputData>();
        Entities.ForEach((Entity e, in SteeringController ctrl, in Interacted interacted) =>
        {
            if (interacted.Interactor != localPlayer)
                return;

            if (input.CurrentAction != InputSystem.ActionState.None)
                return;

            if (HasSingleton<ControlScheme.OnFeet>())
            {
                inputSystem.SetSchemeSteer(e);
            }
            else
            {
                inputSystem.SetSchemeFeet();
            }
        }).WithStructuralChanges().WithoutBurst().Run();
    }
}