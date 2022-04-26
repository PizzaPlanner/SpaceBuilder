using Unity.Entities;
using UnityEngine;

[GenerateAuthoringComponent]
public struct Interactable : IComponentData { }

public struct Interacted : IComponentData
{
    public Entity Interactor;
}

public partial class InteractionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        EntityManager.RemoveComponent<Interacted>(GetEntityQuery(typeof(Interacted)));
        if (Input.GetMouseButtonDown(0))
        {
            var myTarget = GetComponent<Target>(GetSingletonEntity<LocalPlayer>());
            if(myTarget.CanInteract)
                EntityManager.AddComponentData(myTarget.Value, new Interacted{Interactor = GetSingletonEntity<LocalPlayer>()});
        }
    }
}
