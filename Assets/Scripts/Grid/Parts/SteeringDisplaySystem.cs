using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

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
        RequireForUpdate<ControlScheme.Ship>();
        RequireForUpdate<LocalShip>();
        RequireForUpdate<InputData>();
    }

    protected override void OnStartRunning()
    {
        uiRoot = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("SteeringOverlay");
        uiRoot.style.display = DisplayStyle.Flex;
        cursor = uiRoot.Q("Cursor");
        lastMousePos = float2.zero;
        wantedAngle = float2.zero;
    }

    protected override void OnStopRunning()
    {
        uiRoot.style.display = DisplayStyle.None;
    }

    protected override void OnUpdate()
    {
        var input = SystemAPI.GetSingleton<InputData>();
        var steerer = SystemAPI.GetSingleton<LocalShip>().Controller;
        var ctrl = SystemAPI.GetComponent<SteeringController>(steerer);
        
        wantedAngle = input.Cursor * new float2(-1, 1) / SteeringScale;
        wantedAngle = math.clamp(wantedAngle, -1, 1);
        if (math.lengthsq(wantedAngle) > 1)
            wantedAngle = math.normalize(wantedAngle);
        cursor.transform.position = new Vector3(wantedAngle.x * SteeringScale, wantedAngle.y * SteeringScale, 0);
        var angle2D = math.length(wantedAngle) > SteeringDeadzone ? wantedAngle : 0;
        var angleZ = input.Rotation.z * 2;
        ctrl.SteeringAngle = new float3(angle2D.y, angle2D.x, angleZ);
        ctrl.Thrust = input.Movement;

        SystemAPI.SetComponent(steerer, ctrl);
    }
}