using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public struct Door : IComponentData
{
    public bool Open;
    public Entity TheDoor;
    public LocalTransform OpenAnchor, ClosedAnchor;
    public bool Locked;
}

partial struct DoorSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged);
        foreach (var door in SystemAPI.Query<RefRW<Door>>().WithAll<Interacted>())
        {
            if (door.ValueRW.Locked)
                continue;

            door.ValueRW.Open = !door.ValueRW.Open;
            Debug.Log($"Door is now {(door.ValueRW.Open ? "Open" : "Closed")}");
            if(door.ValueRW.Open)
                SystemAPI.SetComponent(door.ValueRW.TheDoor, door.ValueRW.OpenAnchor);
            else
                SystemAPI.SetComponent(door.ValueRW.TheDoor, door.ValueRW.ClosedAnchor);
        }
    }
}