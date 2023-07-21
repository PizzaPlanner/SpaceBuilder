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
        RequireForUpdate<ControlScheme.SteerShip>();
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
        var steerer = SystemAPI.GetSingleton<ControlScheme.SteerShip>();
        var ctrl = SystemAPI.GetComponent<SteeringController>(steerer.Steerer);

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

        SystemAPI.SetComponent(steerer.Steerer, ctrl);
    }
}