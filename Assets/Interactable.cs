using Unity.Entities;
using UnityEngine;

[GenerateAuthoringComponent]
public struct Interactable : IComponentData { }

public struct Interacted : IComponentData { }

public partial class InteractionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        EntityManager.RemoveComponent<Interacted>(GetEntityQuery(typeof(Interacted)));
        if (Input.GetMouseButtonDown(0))
        {
            var myTarget = GetComponent<Target>(GetSingletonEntity<LocalPlayer>());
            if(myTarget.CanInteract)
                EntityManager.AddComponent<Interacted>(myTarget.Value);
        }
    }
}
