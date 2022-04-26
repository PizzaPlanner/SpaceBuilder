using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

public struct BuilderMode_Build : ISystemStateComponentData
{
    public Entity Grid;
}

public struct BuilderMode_Destroy : ISystemStateComponentData
{
    public Entity Grid;
}

public struct CanMove : IComponentData { }
public partial class InputSystem : SystemBase
{
    public struct InputData : ISystemStateComponentData
    {
        public InputSystem.ActionState CurrentAction;
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

    public enum ActionState
    {
        None,
        Construct,
        Deconstruct
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
            uiRoot.Q("BuildMenu").style.display = DisplayStyle.None;
        }
    }

    protected override void OnUpdate()
    {
        var entity = GetSingletonEntity<InputData>();
        var input = GetSingleton<InputData>();

        if (HasSingleton<BuilderMode_Build>() && input.CurrentAction != ActionState.Construct)
            EntityManager.RemoveComponent<BuilderMode_Build>(entity);
        if (HasSingleton<BuilderMode_Destroy>() && input.CurrentAction != ActionState.Deconstruct)
            EntityManager.RemoveComponent<BuilderMode_Destroy>(entity);

        Cursor.lockState = HasSingleton<CanMove>() ? CursorLockMode.Locked : CursorLockMode.None;

        if (Input.GetMouseButtonDown(1))
            input.CurrentAction = ActionState.None;

        var target = GetComponent<Target>(GetSingletonEntity<LocalPlayer>());
        if (target.CanInteract && target.Value != Entity.Null && input.CurrentAction == ActionState.None)
        {
            uiRoot.style.display = DisplayStyle.Flex;
            uiRoot.Q<Label>("MainActionName").text = "Interact";
        }
        else
            uiRoot.style.display = input.CurrentAction != ActionState.None ? DisplayStyle.Flex : DisplayStyle.None;

        input.Scroll = -Input.GetAxis("Mouse ScrollWheel");
        input.ScrollMode = ScrollMode.None;

        if (Input.GetKeyDown(KeyCode.B))
        {
            if (input.CurrentAction == ActionState.Construct)
            {
                input.CurrentAction = ActionState.None;
            }
            else if(input.CurrentAction == ActionState.None && HasComponent<Docked>(GetSingletonEntity<LocalPlayer>()))
            {
                EntityManager.AddComponentData(entity, new BuilderMode_Build{Grid = GetComponent<Docked>(GetSingletonEntity<LocalPlayer>()).Target});
                input.CurrentAction = ActionState.Construct;
                uiRoot.Q<Label>("MainActionName").text = "Construct";
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
            var player = GetSingletonEntity<LocalPlayer>();
            if (HasComponent<CanMove>(player))
                EntityManager.RemoveComponent<CanMove>(player);
            else
                EntityManager.AddComponent<CanMove>(player);
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
            else if (HasSingleton<BuilderMode_Build>())
            {
                if (Input.GetKey(KeyCode.Alpha1) || Input.GetKey(KeyCode.Alpha2) || Input.GetKey(KeyCode.Alpha3))
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
}
