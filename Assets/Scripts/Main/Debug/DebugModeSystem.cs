using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public partial class DebugModeSystem : SystemBase
{
    public struct Active : IComponentData { }

    VisualElement DebugOverlay;

    protected override void OnCreate()
    {
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<IdentifiableMappingSystem.Loaded>();
        RequireForUpdate<InputSystem.ControlUI>();
    }

    protected override void OnStartRunning()
    {
        DebugOverlay ??= Object.FindObjectOfType<UIDocument>().rootVisualElement.Q("DebugUI");
        DebugOverlay.style.display = DisplayStyle.Flex;
        EntityManager.CreateEntity(typeof(Active));
    }

    protected override void OnStopRunning()
    {
        EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<Active>());
        DebugOverlay.style.display = DisplayStyle.None;
    }

    protected override void OnUpdate() { }
}