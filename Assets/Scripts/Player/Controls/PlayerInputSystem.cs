using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(InputSystemGroup))]
partial class PlayerInputSystem : SystemBase
{
    private const float ROTATION_SENSIVITY = .1f;
    protected override void OnCreate()
    {
        RequireForUpdate<ControlScheme.Player>();
    }

    protected override void OnStartRunning() { }

    protected override void OnStopRunning()
    {
        if (SystemAPI.TryGetSingletonEntity<ControlScheme.Building>(out var e))
            EntityManager.DestroyEntity(e);
    }

    protected override void OnUpdate()
    {
        var playerInputEntity = SystemAPI.GetSingletonEntity<ControlScheme.Player>(); 
        if (Input.GetKeyDown(KeyCode.C))
        {
            if (!SystemAPI.HasSingleton<ControlScheme.UI.Inventory>())
                EntityManager.AddComponent<ControlScheme.UI.Inventory>(playerInputEntity);
            else
                EntityManager.RemoveComponent<ControlScheme.UI.Inventory>(playerInputEntity);
        }
        
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (!SystemAPI.TryGetSingletonEntity<ControlScheme.UI.TabMenu>(out var tab))
                EntityManager.CreateEntity(typeof(ControlScheme.UI.TabMenu));
            else
                EntityManager.DestroyEntity(tab);
        }
        
        var isInUI = SystemAPI.HasSingleton<ControlScheme.UI>();
        if (isInUI)
            return;

        var input = SystemAPI.GetSingleton<InputData>();
        var movement = new float3(Input.GetAxis("MoveXAxis"), Input.GetAxis("MoveYAxis"), Input.GetAxis("MoveZAxis"));
        movement = math.normalizesafe(movement);
        input.Movement = movement;
        input.Rotation = new float3(-Input.GetAxis("Mouse Y") * ROTATION_SENSIVITY,
            Input.GetAxis("Mouse X") * ROTATION_SENSIVITY,
            Input.GetAxis("RotateZAxis"));
        input.WantRun = Input.GetKey(KeyCode.LeftShift);
        
        if(Input.GetKeyDown(KeyCode.B))
            if (SystemAPI.TryGetSingletonEntity<ControlScheme.Building>(out var b))
                EntityManager.DestroyEntity(b);
            else
                EntityManager.CreateEntity(typeof(ControlScheme.Building));

        if(!SystemAPI.HasSingleton<ControlScheme.Building>())
            input.WantInteraction = Input.GetMouseButtonDown(0);
        
        SystemAPI.SetSingleton(input);
    }
}