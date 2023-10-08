using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

partial class BuildingTabMenuSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<ControlScheme.Building>();
        RequireForUpdate<ControlScheme.UI.TabMenu>();
    }

    VisualElement _menu;
    
    protected override void OnStartRunning()
    {
        _menu ??= GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("BuildingUI").Q("InMenu");
        _menu.style.display = DisplayStyle.Flex;
    }

    protected override void OnStopRunning()
    {
        _menu.style.display = DisplayStyle.None;
    }

    protected override void OnUpdate() { }
}