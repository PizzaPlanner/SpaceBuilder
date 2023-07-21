using Unity.Entities;
using Unity.Physics.Authoring;
using UnityEngine;
using UnityEngine.UIElements;

public partial class DebugButtonsSystem : SystemBase
{
    VisualElement addSpeed, showColliders, targetFilter;
    
    protected override void OnCreate()
    {
        RequireForUpdate<DebugModeSystem.Active>();
    }
    
    protected override void OnStartRunning()
    {
        var root = Object.FindObjectOfType<UIDocument>().rootVisualElement.Q("DebugUI");
        var player = SystemAPI.GetSingletonEntity<LocalPlayer>();
        if (addSpeed == null)
        {
            addSpeed = root.Q("MoreSpeed");
            addSpeed.RegisterCallback<MouseDownEvent>(x =>
            {
                var cts = EntityManager.GetComponentData<Player>(player);
                if (x.button == 0)
                    cts.MovementSpeedRun *= 10;
                else
                    cts.MovementSpeedRun /= 10;
                (addSpeed as Label)!.text = $"±Speed ({GalaxyTools.ToGalacticDistanceWithUnit(cts.MovementSpeedRun)})";
                EntityManager.SetComponentData(player, cts);
            });
        }
        
        if (showColliders == null)
        {
            showColliders = root.Q("Colliders");
            showColliders.RegisterCallback<MouseDownEvent>(x =>
            {
                wantToggleColliders = true;
            });
        }
        
        if (targetFilter == null)
        {
            targetFilter = root.Q<EnumField>("TargetFilter");
            targetFilter.RegisterCallback<ChangeEvent<PlayerTargetSystem.PlayerTargetMode>>(x =>
            {
                newTargetMode = x.newValue;
                wantUpdateTM = true;
            });
        }
    }

    bool wantToggleColliders;
    PlayerTargetSystem.PlayerTargetMode newTargetMode;
    bool wantUpdateTM;

    protected override void OnUpdate()
    {
        if (wantToggleColliders)
        {
            Debug.Log($"Toggle colliders");
            wantToggleColliders = false;
            if (SystemAPI.TryGetSingletonEntity<PhysicsDebugDisplayData>(out var debug))
                EntityManager.DestroyEntity(debug);
            else
            {
                EntityManager.CreateEntity(typeof(PhysicsDebugDisplayData));
                SystemAPI.SetSingleton(new PhysicsDebugDisplayData { DrawColliders = 1, DrawColliderEdges = 1 });
            }
        }

        if (wantUpdateTM)
        {
            wantUpdateTM = false;
            SystemAPI.SetSingleton(new PlayerTargetSystem.PlayerTargetModeSettings { Mode = newTargetMode });
        }
    }
}