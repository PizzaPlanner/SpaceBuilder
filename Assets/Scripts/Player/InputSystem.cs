using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

public struct BuilderMode_Build : ICleanupComponentData { }

public struct BuilderMode_Blueprint : ICleanupComponentData { }

public struct BuilderMode_Destroy : ICleanupComponentData { }

public static class ControlScheme
{
    public struct OnFeet : ICleanupComponentData { }
    public struct SteerShip : ICleanupComponentData
    {
        public Entity Steerer;
    }
}

public partial class InputSystem : SystemBase
{
    public struct InputData : ICleanupComponentData
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
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<InputData>();
        EntityManager.CreateEntity(typeof(InputData), typeof(ControlScheme.OnFeet));
        EntityManager.CreateEntity(typeof(ControlPlayer));
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
        var entity = SystemAPI.GetSingletonEntity<InputData>();
        var input = SystemAPI.GetSingleton<InputData>();

        if (SystemAPI.HasSingleton<BuilderMode_Build>() && input.CurrentAction != ActionState.Construct)
            EntityManager.RemoveComponent<BuilderMode_Build>(entity);
        if (SystemAPI.HasSingleton<BuilderMode_Destroy>() && input.CurrentAction != ActionState.Deconstruct)
            EntityManager.RemoveComponent<BuilderMode_Destroy>(entity);
        if (SystemAPI.HasSingleton<BuilderMode_Blueprint>() && input.CurrentAction != ActionState.Blueprint)
            EntityManager.RemoveComponent<BuilderMode_Blueprint>(entity);

        if (Input.GetMouseButtonDown(1))
        {
            input.CurrentAction = ActionState.None;
            if(SystemAPI.HasSingleton<ControlScheme.SteerShip>())
                SetSchemeFeet();
        }

        var target = SystemAPI.GetComponent<Target>(SystemAPI.GetSingletonEntity<LocalPlayer>());
        if (SystemAPI.HasSingleton<ControlPlayer>() && target.CanInteract && target.Value != Entity.Null && input.CurrentAction == ActionState.None)
        {
            uiRoot.style.display = DisplayStyle.Flex;
            uiRoot.Q<Label>("MainActionName").text = "Interact";
        }
        else if (SystemAPI.HasSingleton<ControlPlayer>() && target.CanConstruct && target.Value != Entity.Null && input.CurrentAction == ActionState.Construct)
        {
            uiRoot.style.display = DisplayStyle.Flex;
            uiRoot.Q<Label>("MainActionName").text = "Construct";
        }
        else
            uiRoot.style.display = DisplayStyle.None;

        input.Scroll = -Input.GetAxis("Mouse ScrollWheel");
        input.ScrollMode = ScrollMode.None;

        if (SystemAPI.HasSingleton<ControlPlayer>())
        {
            if (Input.GetKeyDown(KeyCode.B))
            {
                if (input.CurrentAction == ActionState.Blueprint)
                {
                    input.CurrentAction = ActionState.None;
                }
                else if (input.CurrentAction == ActionState.None)
                {
                    EntityManager.AddComponentData(entity, new BuilderMode_Blueprint());
                    input.CurrentAction = ActionState.Blueprint;
                }
            }

            if (Input.GetKeyDown(KeyCode.V))
            {
                if (input.CurrentAction == ActionState.Construct)
                {
                    input.CurrentAction = ActionState.None;
                }
                else if (input.CurrentAction == ActionState.None)
                {
                    EntityManager.AddComponentData(entity, new BuilderMode_Build());
                    input.CurrentAction = ActionState.Construct;
                }
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                if (input.CurrentAction == ActionState.Deconstruct)
                {
                    input.CurrentAction = ActionState.None;
                }
                else if (input.CurrentAction == ActionState.None)
                {
                    EntityManager.AddComponentData(entity, new BuilderMode_Destroy());
                    input.CurrentAction = ActionState.Deconstruct;
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (SystemAPI.HasSingleton<ControlPlayer>())
                SetCursor(CursorState.Default);
            else if(SystemAPI.HasSingleton<ControlUI>())
                SetCursor(CursorState.Disabled);
        }

        if (input.Scroll != 0)
        {
            if (Input.GetKey(KeyCode.F2))
            {
                input.ScrollMode = ScrollMode.Camera;
                var anchor = SystemAPI.GetSingleton<CameraAnchor>();
                anchor.ScrollFactor += input.Scroll;
                anchor.ScrollFactor = math.clamp(anchor.ScrollFactor, 0, 2);
                SystemAPI.SetSingleton(anchor);
            }
            else if (SystemAPI.HasSingleton<BuilderMode_Build>() || SystemAPI.HasSingleton<BuilderMode_Blueprint>())
            {
                if (!Input.GetMouseButton(0) && (Input.GetKey(KeyCode.Alpha1) || Input.GetKey(KeyCode.Alpha2) || Input.GetKey(KeyCode.Alpha3)))
                    input.ScrollMode = ScrollMode.Rotate;
                else
                    input.ScrollMode = ScrollMode.Context;
            }
            else if (SystemAPI.HasSingleton<BuilderMode_Destroy>())
                input.ScrollMode = ScrollMode.Context;
            else
                input.ScrollMode = ScrollMode.Inventory;
        }

        if (Input.GetKeyDown(KeyCode.F1))
        {
            var targetGrid = SystemAPI.GetComponent<Target>(SystemAPI.GetSingletonEntity<LocalPlayer>()).GridHit.Grid;
            if (targetGrid == Entity.Null)
                return;
            
            // var bp = BlueprintUtils.Save(targetGrid, EntityManager);
#if UNITY_EDITOR
            // var name = GetComponent<Name>(targetGrid).Value.Value;
            // File.WriteAllText(Application.dataPath + $"/Blueprints/{name}.txt", bp);
            // Debug.Log($"Saved Blueprint {name}");
#endif
        }

        SystemAPI.SetSingleton(input);
    }


    public struct ControlUI : IComponentData { }
    public struct ControlHotMenu : IComponentData { }
    public struct ControlPlayer : IComponentData { }
    public void SetCursor(CursorState state)
    {
        if(SystemAPI.HasSingleton<ControlUI>())
            EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<ControlUI>());
        if (SystemAPI.HasSingleton<ControlPlayer>())
            EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<ControlPlayer>());
        if (SystemAPI.HasSingleton<ControlHotMenu>())
            EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<ControlHotMenu>());

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

    public void SetSchemeSteer(Entity target)
    {
        var input = SystemAPI.GetSingletonEntity<InputData>();
        EntityManager.RemoveComponent<ControlScheme.OnFeet>(input);
        EntityManager.AddComponentData(input, new ControlScheme.SteerShip{Steerer = target});
        SetCursor(CursorState.Default);
    }

    public void SetSchemeFeet()
    {
        var input = SystemAPI.GetSingletonEntity<InputData>();
        EntityManager.RemoveComponent<ControlScheme.SteerShip>(input);
        EntityManager.AddComponent<ControlScheme.OnFeet>(input);
        SetCursor(CursorState.Disabled);
    }
}
