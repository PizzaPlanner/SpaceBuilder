using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

[UpdateInGroup(typeof(InputSystemGroup))]
partial class ShipInputSystem : SystemBase
{
    private const float ROTATION_SENSIVITY = .1f;
    protected override void OnCreate()
    {
        RequireForUpdate<ControlScheme.Ship>();
    }

    IPanel uiRootPanel;
    protected override void OnStartRunning()
    {
        uiRootPanel ??= GameObject.FindObjectOfType<UIDocument>().rootVisualElement.panel;
    }

    protected override void OnStopRunning() { }

    protected override void OnUpdate()
    {
        var isInUI = SystemAPI.HasSingleton<ControlScheme.UI>();
        if (isInUI)
            return;

        var input = SystemAPI.GetSingleton<InputData>();
        var movement = new float3(Input.GetAxis("MoveXAxis"), Input.GetAxis("MoveYAxis"), Input.GetAxis("MoveZAxis"));
        movement = math.normalizesafe(movement);
        input.Movement = movement;
        input.Rotation = new float3(0, 0,
            Input.GetAxis("RotateZAxis"));
        input.WantRun = Input.GetKey(KeyCode.LeftShift);
        input.WantCancel = Input.GetMouseButtonDown(1);
        var cursorOnPanel = RuntimePanelUtils.ScreenToPanel(uiRootPanel, ((float3)Input.mousePosition).xy);
        var cam = Camera.main;
        var center =
            RuntimePanelUtils.CameraTransformWorldToPanel(uiRootPanel, cam.transform.position + cam.transform.forward,
                cam);
        input.Cursor = center - cursorOnPanel;
        

        SystemAPI.SetSingleton(input);
    }
}