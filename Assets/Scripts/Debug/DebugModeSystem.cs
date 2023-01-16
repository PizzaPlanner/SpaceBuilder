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
    }

    protected override void OnStartRunning()
    {
        DebugOverlay ??= GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("DebugUI");
        DebugOverlay.style.display = DisplayStyle.None;
    }

    protected override void OnUpdate()
    {
        if (!Input.GetKeyDown(KeyCode.F5))
            return;
        
        if (SystemAPI.TryGetSingletonEntity<Active>(out var e))
        {
            EntityManager.DestroyEntity(e);
            DebugOverlay.style.display = DisplayStyle.None;
        }
        else
        {
            EntityManager.CreateEntity(typeof(Active));
            DebugOverlay.style.display = DisplayStyle.Flex;
        }
    }
}