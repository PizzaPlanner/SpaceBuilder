using System;
using System.Diagnostics;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;
using Debug = UnityEngine.Debug;

public struct BuilderMode_Build : ISystemStateComponentData
{
    public Entity Grid;
}

public struct BuilderMode_Blueprint : ISystemStateComponentData
{
    public Entity Grid;
}

public struct BuilderMode_Destroy : ISystemStateComponentData
{
    public Entity Grid;
}

public partial class InputSystem : SystemBase
{
    public struct InputData : ISystemStateComponentData
    {
        public ActionState CurrentAction;
        public ScrollMode ScrollMode;
        public float Scroll;
    }

    public enum ScrollMode
    {
        None,
        Camera,
        Inventory,
        Context,
        Rotate
    }

    public enum CursorState
    {
        Disabled,
        Default,
        HotMenu
    }

    public enum ActionState
    {
        None,
        Construct,
        Deconstruct,
        Blueprint
    }

    private VisualElement uiRoot;
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
        RequireSingletonForUpdate<InputData>();
        EntityManager.CreateEntity(typeof(InputData));
    }

    protected override void OnStartRunning()
    {
        if (uiRoot == null)
        {
            uiRoot = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("ContextMenu");
            uiRoot.style.display = DisplayStyle.None;
        }
        SetCursor(CursorState.Disabled);
    }

    protected override void OnUpdate()
    {
        var entity = GetSingletonEntity<InputData>();
        var input = GetSingleton<InputData>();

        if (HasSingleton<BuilderMode_Build>() && input.CurrentAction != ActionState.Construct)
            EntityManager.RemoveComponent<BuilderMode_Build>(entity);
        if (HasSingleton<BuilderMode_Destroy>() && input.CurrentAction != ActionState.Deconstruct)
            EntityManager.RemoveComponent<BuilderMode_Destroy>(entity);
        if (HasSingleton<BuilderMode_Blueprint>() && input.CurrentAction != ActionState.Blueprint)
            EntityManager.RemoveComponent<BuilderMode_Blueprint>(entity);

        if (Input.GetMouseButtonDown(1))
            input.CurrentAction = ActionState.None;

        var target = GetComponent<Target>(GetSingletonEntity<LocalPlayer>());
        if (target.CanInteract && target.Value != Entity.Null && input.CurrentAction == ActionState.None)
        {
            uiRoot.style.display = DisplayStyle.Flex;
            uiRoot.Q<Label>("MainActionName").text = "Interact";
        }
        else
            uiRoot.style.display = DisplayStyle.None;

        input.Scroll = -Input.GetAxis("Mouse ScrollWheel");
        input.ScrollMode = ScrollMode.None;

        if (Input.GetKeyDown(KeyCode.B))
        {
            if (input.CurrentAction == ActionState.Blueprint)
            {
                input.CurrentAction = ActionState.None;
            }
            else if(input.CurrentAction == ActionState.None && HasComponent<Docked>(GetSingletonEntity<LocalPlayer>()))
            {
                EntityManager.AddComponentData(entity, new BuilderMode_Blueprint{Grid = GetComponent<Docked>(GetSingletonEntity<LocalPlayer>()).Target});
                input.CurrentAction = ActionState.Blueprint;
                uiRoot.Q<Label>("MainActionName").text = "Design";
            }
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            if (input.CurrentAction == ActionState.Deconstruct)
            {
                input.CurrentAction = ActionState.None;
            }
            else if(input.CurrentAction == ActionState.None && HasComponent<Docked>(GetSingletonEntity<LocalPlayer>()))
            {
                EntityManager.AddComponentData(entity, new BuilderMode_Destroy{Grid = GetComponent<Docked>(GetSingletonEntity<LocalPlayer>()).Target});
                input.CurrentAction = ActionState.Deconstruct;
                uiRoot.Q<Label>("MainActionName").text = "Deconstruct";
            }
        }

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (HasSingleton<ControlPlayer>())
                SetCursor(CursorState.Default);
            else if(HasSingleton<ControlUI>())
                SetCursor(CursorState.Disabled);
        }

        if (input.Scroll != 0)
        {
            if (Input.GetKey(KeyCode.F2))
            {
                input.ScrollMode = ScrollMode.Camera;
                var anchor = GetSingleton<CameraAnchor>();
                anchor.ScrollFactor += input.Scroll;
                anchor.ScrollFactor = math.clamp(anchor.ScrollFactor, 0, 2);
                SetSingleton(anchor);
            }
            else if (HasSingleton<BuilderMode_Build>() || HasSingleton<BuilderMode_Blueprint>())
            {
                if (!Input.GetMouseButton(0) && (Input.GetKey(KeyCode.Alpha1) || Input.GetKey(KeyCode.Alpha2) || Input.GetKey(KeyCode.Alpha3)))
                    input.ScrollMode = ScrollMode.Rotate;
                else
                    input.ScrollMode = ScrollMode.Context;
            }
            else if (HasSingleton<BuilderMode_Destroy>())
                input.ScrollMode = ScrollMode.Context;
            else
                input.ScrollMode = ScrollMode.Inventory;
        }

        if (Input.GetKeyDown(KeyCode.F1) && HasComponent<Docked>(GetSingletonEntity<LocalPlayer>()))
        {
            Debug.Log(BlueprintUtils.Save(GetComponent<Docked>(GetSingletonEntity<LocalPlayer>()).Target, EntityManager));
        }

        SetSingleton(input);
    }


    public struct ControlUI : IComponentData { }
    public struct ControlHotMenu : IComponentData { }
    public struct ControlPlayer : IComponentData { }
    public void SetCursor(CursorState state)
    {
        if(HasSingleton<ControlUI>())
            EntityManager.DestroyEntity(GetSingletonEntity<ControlUI>());
        if (HasSingleton<ControlPlayer>())
            EntityManager.DestroyEntity(GetSingletonEntity<ControlPlayer>());
        if (HasSingleton<ControlHotMenu>())
            EntityManager.DestroyEntity(GetSingletonEntity<ControlHotMenu>());

        switch (state)
        {
            case CursorState.Disabled:
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                EntityManager.CreateEntity(typeof(ControlPlayer));
                break;
            case CursorState.Default:
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                EntityManager.CreateEntity(typeof(ControlUI));
                break;
            case CursorState.HotMenu:
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                EntityManager.CreateEntity(typeof(ControlHotMenu));
                break;
        }
    }
}
