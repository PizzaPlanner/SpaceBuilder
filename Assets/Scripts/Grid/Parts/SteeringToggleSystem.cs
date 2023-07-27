using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

public partial class SteeringToggleSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<ControlScheme.Player>();
    }

    protected override void OnUpdate()
    {
        var localPlayer = SystemAPI.GetSingletonEntity<LocalPlayer>();
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach(var (ctrl, parent, interacted, e) in 
                SystemAPI.Query<SteeringController, Parent, Interacted>().WithEntityAccess())
        {
            if (interacted.Interactor != localPlayer)
                return;

            if (!SystemAPI.HasComponent<ReferenceFrame>(parent.Value))
                return;
            
            ecb.AddComponent<LocalShip>(e);
        }

        ecb.Playback(EntityManager);
    }
}