using System.Linq;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public partial class AdminConsoleSystem : SystemBase
{
    private VisualElement DebugOverlay;
    protected override void OnCreate()
    {
        RequireForUpdate<LocalPlayer>();
        RequireForUpdate<IdentifiableMappingSystem.Loaded>();
        RequireForUpdate<ControlScheme.UI>();
        
        AdminConsole.Register(new GiveItemConsoleCommand{ GetEcb = GetCommandBuffer });
        AdminConsole.Register(new NoclipConsoleCommand{EntityManager = EntityManager});
        AdminConsole.Register(new SpawnCommand{EntityManager = EntityManager});
        AdminConsole.Register(new MoveSectorCommand{EntityManager = EntityManager});
    }

    private AdminConsole uiRoot;
    protected override void OnStartRunning()
    {
        if (uiRoot == null)
        {
            uiRoot = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q<AdminConsole>();
            uiRoot.LocalPlayer = SystemAPI.GetSingletonEntity<LocalPlayer>();
        }
        uiRoot.style.display = DisplayStyle.None;
    }

    EntityCommandBuffer GetCommandBuffer()
    {
        return SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
    }

    protected override void OnUpdate()
    {
        if (Input.GetKeyDown(KeyCode.F4))
        {
            uiRoot.style.display = 
                uiRoot.style.display == DisplayStyle.Flex 
                    ? DisplayStyle.None 
                    : DisplayStyle.Flex;
            if (uiRoot.style.display == DisplayStyle.Flex)
            {
                uiRoot.Q<TextField>().Focus();
            }
        }
    }
}