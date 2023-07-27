using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(InputSystemGroup))]
partial class BuildingInputSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<ControlScheme.Building>();
    }

    protected override void OnStartRunning()
    {
        var b = SystemAPI.GetSingletonEntity<ControlScheme.Building>();
        EntityManager.AddComponent<ControlScheme.Building.Build>(b);
    }

    protected override void OnStopRunning()
    {
        if(SystemAPI.TryGetSingletonEntity<ControlScheme.UI.BuildingParts>(out var e))
            EntityManager.DestroyEntity(e);
    }

    protected override void OnUpdate()
    {
        if (Input.GetKeyDown(KeyCode.T) && !SystemAPI.HasSingleton<ControlScheme.UI.BuildingParts>())
            EntityManager.CreateEntity(typeof(ControlScheme.UI.BuildingParts));
        else if(Input.GetKeyUp(KeyCode.T) && SystemAPI.TryGetSingletonEntity<ControlScheme.UI.BuildingParts>(out var p))
            EntityManager.DestroyEntity(p);
        
        var isInUI = SystemAPI.HasSingleton<ControlScheme.UI>();
        if (isInUI)
            return;

        var input = SystemAPI.GetSingleton<InputData>();

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            if (SystemAPI.TryGetSingletonEntity<ControlScheme.Building.Build>(out var b))
            {
                EntityManager.RemoveComponent<ControlScheme.Building.Build>(b);
                EntityManager.AddComponent<ControlScheme.Building.Destroy>(b);
            }
            else if(SystemAPI.TryGetSingletonEntity<ControlScheme.Building.Destroy>(out b))
            {
                EntityManager.RemoveComponent<ControlScheme.Building.Destroy>(b);
                EntityManager.AddComponent<ControlScheme.Building.Build>(b);
            }
        }
            
        input.WantBuild = Input.GetMouseButtonUp(0);
        input.WantDrag = Input.GetMouseButton(0);

        var build = SystemAPI.HasSingleton<ControlScheme.Building.Build>();
        if (build && Input.GetKeyDown(KeyCode.R))
        {
            input.Scroll = Input.GetKey(KeyCode.LeftShift) ? -1 : 1;
            input.ScrollMode = ScrollMode.Rotate;
        }
        else if (build && Input.mouseScrollDelta.y != 0)
        {
            input.Scroll = math.sign(Input.mouseScrollDelta.y);
            input.ScrollMode = ScrollMode.Scale;
        }

        SystemAPI.SetSingleton(input);
    }
}