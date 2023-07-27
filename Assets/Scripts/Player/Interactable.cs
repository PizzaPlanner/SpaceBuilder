using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public struct Interactable : IComponentData { }

public struct Interacted : IComponentData
{
    public Entity Interactor;
}

public partial class InteractionSystem : SystemBase
{
    VisualElement interactableIndicator;
    protected override void OnCreate()
    {
        RequireForUpdate<ControlScheme.Player>();
    }

    protected override void OnStartRunning()
    {
        interactableIndicator ??= GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("ContextMenu");
        interactableIndicator.style.display = DisplayStyle.None;
    }

    protected override void OnStopRunning()
    {
        EntityManager.RemoveComponent<Interacted>(GetEntityQuery(typeof(Interacted)));
        interactableIndicator.style.display = DisplayStyle.None;
    }

    protected override void OnUpdate()
    {
        EntityManager.RemoveComponent<Interacted>(GetEntityQuery(typeof(Interacted)));
        var myTarget = SystemAPI.GetComponent<Target>(SystemAPI.GetSingletonEntity<LocalPlayer>());
        interactableIndicator.style.display = myTarget.CanInteract ? DisplayStyle.Flex : DisplayStyle.None;
        if (myTarget.CanInteract)
            interactableIndicator.Q<Label>().text = "Interact";
        if (myTarget.CanInteract && SystemAPI.GetSingleton<InputData>().WantInteraction)
        {
            EntityManager.AddComponentData(myTarget.Value, new Interacted
            {
                Interactor = SystemAPI.GetSingletonEntity<LocalPlayer>()
            });
        }
    }
}
