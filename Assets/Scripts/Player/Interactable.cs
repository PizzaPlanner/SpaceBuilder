using Unity.Entities;
using UnityEngine;

public struct Interactable : IComponentData { }

public struct Interacted : IComponentData
{
    public Entity Interactor;
}

public partial class InteractionSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<LocalPlayer>();
    }

    protected override void OnUpdate()
    {
        EntityManager.RemoveComponent<Interacted>(GetEntityQuery(typeof(Interacted)));
        if (Input.GetMouseButtonDown(0))
        {
            var myTarget = SystemAPI.GetComponent<Target>(SystemAPI.GetSingletonEntity<LocalPlayer>());
            if (myTarget.CanInteract)
                EntityManager.AddComponentData(myTarget.Value, new Interacted
                {
                    Interactor = SystemAPI.GetSingletonEntity<LocalPlayer>()
                });
        }
    }
}
