using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using UnityEngine.UIElements;

[GenerateAuthoringComponent]
public struct SteeringController : IComponentData
{
    public float3 SteeringAngle;
    public float3 Thrust;
}

public partial class SteeringSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((in SteeringController steer, in GridItem item, in DynamicBuffer<ControlledItems> items) =>
        {
            var grid = item.Grid;
            if (!HasComponent<Flyable>(grid))
                return;

            var velocity = GetComponent<PhysicsVelocity>(grid);
            velocity.Angular = steer.SteeringAngle;
            SetComponent(grid, velocity);
            for (int i = 0; i < items.Length; i++)
            {
                var target = items[i].Target;
                if (!HasComponent<Thruster>(target))
                    continue;

                var thruster = GetComponent<Thruster>(target);
                if (items[i].Type == ToggleType.ThrusterForward)
                    thruster.Strength = steer.Thrust.z > 0 ? steer.Thrust.z : 0;
                if (items[i].Type == ToggleType.ThrusterBackward)
                    thruster.Strength = steer.Thrust.z < 0 ? -steer.Thrust.z : 0;
                if (items[i].Type == ToggleType.ThrusterUp)
                    thruster.Strength = steer.Thrust.y > 0 ? steer.Thrust.y : 0;
                if (items[i].Type == ToggleType.ThrusterDown)
                    thruster.Strength = steer.Thrust.y < 0 ? -steer.Thrust.y : 0;
                if (items[i].Type == ToggleType.ThrusterRight)
                    thruster.Strength = steer.Thrust.x > 0 ? steer.Thrust.x : 0;
                if (items[i].Type == ToggleType.ThrusterLeft)
                    thruster.Strength = steer.Thrust.x < 0 ? -steer.Thrust.x : 0;
                thruster.Strength = math.clamp(thruster.Strength, 0, 1);
                SetComponent(target, thruster);
            }
        }).Schedule();
    }
}

public partial class SteeringToggleSystem : SystemBase
{
    private InputSystem inputSystem;

    protected override void OnCreate()
    {
        inputSystem = World.GetExistingSystem<InputSystem>();
        RequireSingletonForUpdate<LocalPlayer>();
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

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class SteeringDisplaySystem : SystemBase
{
    private VisualElement uiRoot, cursor;
    private readonly float SteeringScale = 128;
    private readonly float SteeringDeadzone = 0.1f;
    private readonly float Precision = 0.2f;
    private float2 lastMousePos;
    private float2 wantedAngle;

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<ControlScheme.SteerShip>();
    }

    protected override void OnStartRunning()
    {
        uiRoot = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("SteeringOverlay");
        uiRoot.style.display = DisplayStyle.Flex;
        cursor = uiRoot.Q("Cursor");
        lastMousePos = new float2(Input.mousePosition.x, Input.mousePosition.y);
        wantedAngle = float2.zero;
    }

    protected override void OnStopRunning()
    {
        uiRoot.style.display = DisplayStyle.None;
    }

    protected override void OnUpdate()
    {
        var steerer = GetSingleton<ControlScheme.SteerShip>();
        var ctrl = GetComponent<SteeringController>(steerer.Steerer);

        var mouseDelta = new float2(Input.mousePosition.x, Input.mousePosition.y) - lastMousePos;
        lastMousePos = lastMousePos + mouseDelta;
        mouseDelta.y *= -1;
        wantedAngle += mouseDelta / SteeringScale * Precision;
        wantedAngle = math.clamp(wantedAngle, -1, 1);
        if (math.lengthsq(wantedAngle) > 1)
            wantedAngle = math.normalize(wantedAngle);
        cursor.transform.position = new Vector3(wantedAngle.x * SteeringScale, wantedAngle.y * SteeringScale, 0);
        var angle2D = math.length(wantedAngle) > SteeringDeadzone ? wantedAngle : 0;
        var angleZ = Input.GetAxis("RotateZAxis") * 2;
        ctrl.SteeringAngle = new float3(angle2D.y, angle2D.x, angleZ);

        var movement = new float3(Input.GetAxis("MoveXAxis"), Input.GetAxis("MoveYAxis"), Input.GetAxis("MoveZAxis"));
        ctrl.Thrust = movement;

        SetComponent(steerer.Steerer, ctrl);
    }
}
