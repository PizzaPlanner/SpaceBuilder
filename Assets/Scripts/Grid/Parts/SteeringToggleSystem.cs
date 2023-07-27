using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public partial class SteeringToggleSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<LocalPlayer>();
    }

    protected override void OnUpdate()
    {
        var localPlayer = SystemAPI.GetSingletonEntity<LocalPlayer>();
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        if (SystemAPI.HasSingleton<ControlScheme.Ship>())
        {
            if (SystemAPI.GetSingleton<InputData>().WantCancel
                && SystemAPI.TryGetSingletonEntity<LocalShip>(out var ship))
            {
                ecb.RemoveComponent<LocalShip>(ship);
            }
        }
        else
        {
            foreach(var (ctrl, parent, interacted, e) in 
                    SystemAPI.Query<SteeringController, Parent, Interacted>().WithEntityAccess())
            {
                if (interacted.Interactor != localPlayer)
                    return;

                if (!SystemAPI.HasComponent<ReferenceFrameOrigin>(parent.Value))
                    return;

                ecb.AddComponent(parent.Value, new LocalShip { Controller = e });
            }
        }
        
        ecb.Playback(EntityManager);
    }
}