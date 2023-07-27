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
        RequireForUpdate<InputData>();
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
        var input = SystemAPI.GetSingleton<InputData>();
        var steerer = Entity.Null;
        var ctrl = SystemAPI.GetComponent<SteeringController>(steerer);

        var mouseDelta = input.Rotation.xy;
        lastMousePos += mouseDelta;
        mouseDelta.y *= -1;
        wantedAngle += lastMousePos / SteeringScale * Precision;
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