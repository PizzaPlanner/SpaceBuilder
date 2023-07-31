using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

public static class ControlScheme
{
    public struct Building : IComponentData
    {
        public struct Build : IComponentData { }
        public struct Destroy : IComponentData { }
    }
    
    public struct Ship : IComponentData { }
    public struct Player : IComponentData { }

    public struct UI : IComponentData
    {
        public struct MainMenu : IComponentData { }
        public struct TabMenu : IComponentData { }
        public struct Inventory : IComponentData { }
        public struct BuildingParts : IComponentData { }
        public struct LogicDisplay : IComponentData { }
    }
}
public struct InputData : ICleanupComponentData
{
    public ScrollMode ScrollMode;
    public float Scroll;
    public float3 Rotation;
    public float2 Cursor;
    public float3 Movement;
    public bool WantInteraction;
    public bool WantBuild;
    public bool WantDrag;
    public bool WantRun;
    public bool WantCancel;
}
public enum ScrollMode
{
    None,
    Camera,
    Inventory,
    Context,
    Rotate,
    Scale
}

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class InputSystemGroup : ComponentSystemGroup
{
    private VisualElement uiRoot;
    protected override void OnCreate()
    {
        base.OnCreate();
        EntityManager.CreateEntity(typeof(InputData));
    }

    protected override void OnStartRunning()
    {
        uiRoot ??= GameObject.FindObjectOfType<UIDocument>().rootVisualElement;
        
        // FIXME: Do this properly maybe? Workaround for not being able to tab out of UI sometimes;
        // Should no longer be required when not using Editor Elements;
        foreach (var element in uiRoot.Query<VisualElement>().Build())
        {
            element.tabIndex = -1;
        }
    }

    protected override void OnUpdate()
    {
        var input = new InputData();
        SystemAPI.SetSingleton(input);

        var schemeMissing = !SystemAPI.HasSingleton<ControlScheme.Ship>() &&
                            !SystemAPI.HasSingleton<ControlScheme.Player>();
        
        if (schemeMissing && SystemAPI.HasSingleton<LocalShip>())
            EntityManager.CreateEntity(typeof(ControlScheme.Ship));
        else if (schemeMissing && SystemAPI.HasSingleton<LocalPlayer>())
            EntityManager.CreateEntity(typeof(ControlScheme.Player));

        var isInShip = SystemAPI.TryGetSingletonEntity<LocalShip>(out var ship);
        if (isInShip && SystemAPI.TryGetSingletonEntity<ControlScheme.Player>(out var p))
        {
            EntityManager.DestroyEntity(p);
        }
        
        if (!isInShip && SystemAPI.TryGetSingletonEntity<ControlScheme.Ship>(out var s))
        {
            EntityManager.DestroyEntity(s);
        }
        
        var isAnyUiElementFocussed = uiRoot.focusController.focusedElement != null;
        if (!isAnyUiElementFocussed)
            base.OnUpdate();

        SetCursor();
    }

    public void SetCursor()
    {
        if (SystemAPI.HasSingleton<ControlScheme.UI>() || SystemAPI.HasSingleton<ControlScheme.Ship>())
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
