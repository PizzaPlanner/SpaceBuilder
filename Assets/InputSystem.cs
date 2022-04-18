using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public struct BuilderMode_Build : ISystemStateComponentData { }
public struct BuilderMode_Destroy : ISystemStateComponentData { }
public class InputSystem : SystemBase
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
    }

    protected override void OnUpdate()
    {
        var entity = GetSingletonEntity<InputData>();
        var input = GetSingleton<InputData>();

        if (HasSingleton<BuilderMode_Build>() && input.CurrentAction != ActionState.Construct)
            EntityManager.RemoveComponent<BuilderMode_Build>(entity);
        if (HasSingleton<BuilderMode_Destroy>() && input.CurrentAction != ActionState.Deconstruct)
            EntityManager.RemoveComponent<BuilderMode_Destroy>(entity);

        uiRoot.style.display = input.CurrentAction != ActionState.None ? DisplayStyle.Flex : DisplayStyle.None;

        if (Input.GetMouseButtonDown(1))
            input.CurrentAction = ActionState.None;

        if (Input.GetKeyDown(KeyCode.B))
        {
            if (HasSingleton<BuilderMode_Build>())
            {
                input.CurrentAction = ActionState.None;
            }
            else if(input.CurrentAction == ActionState.None)
            {
                EntityManager.AddComponent<BuilderMode_Build>(entity);
                input.CurrentAction = ActionState.Construct;
                uiRoot.Q<Label>("MainActionName").text = "Construct";
            }
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            if (input.CurrentAction == ActionState.Deconstruct)
            {
                EntityManager.RemoveComponent<BuilderMode_Destroy>(entity);
            }
            else if(input.CurrentAction == ActionState.None)
            {
                EntityManager.AddComponent<BuilderMode_Destroy>(entity);
                input.CurrentAction = ActionState.Deconstruct;
                uiRoot.Q<Label>("MainActionName").text = "Deconstruct";
            }
        }

        SetSingleton(input);
    }
}
