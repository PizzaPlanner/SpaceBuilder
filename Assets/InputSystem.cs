using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

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
    }
    public enum ActionState
    {
        None,
        Construct,
        Deconstruct
    }

    // private VisualElement uiRoot;
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
        RequireSingletonForUpdate<InputData>();
        EntityManager.CreateEntity(typeof(InputData));
    }

    protected override void OnStartRunning()
    {
        // if (uiRoot == null)
        // {
        //     uiRoot = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("ContextMenu");
        //     uiRoot.style.display = DisplayStyle.None;
        // }
    }

    protected override void OnUpdate()
    {
        var entity = GetSingletonEntity<InputData>();
        var input = GetSingleton<InputData>();

        if (HasSingleton<BuilderMode_Build>() && input.CurrentAction != ActionState.Construct)
            EntityManager.RemoveComponent<BuilderMode_Build>(entity);
        if (HasSingleton<BuilderMode_Destroy>() && input.CurrentAction != ActionState.Deconstruct)
            EntityManager.RemoveComponent<BuilderMode_Destroy>(entity);

        // uiRoot.style.display = input.CurrentAction != ActionState.None ? DisplayStyle.Flex : DisplayStyle.None;

        if (Input.GetMouseButtonDown(1))
            input.CurrentAction = ActionState.None;

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
                // uiRoot.Q<Label>("MainActionName").text = "Construct";
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
                // uiRoot.Q<Label>("MainActionName").text = "Deconstruct";
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

        if (Input.GetKeyDown(KeyCode.F1) && HasComponent<Docked>(GetSingletonEntity<LocalPlayer>()))
        {
            Debug.Log(BlueprintUtils.Save(GetComponent<Docked>(GetSingletonEntity<LocalPlayer>()).Target, EntityManager));
        }

        SetSingleton(input);
    }
}
