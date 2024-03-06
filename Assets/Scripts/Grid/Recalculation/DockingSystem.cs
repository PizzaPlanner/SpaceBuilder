using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public partial struct DockingSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ReferenceFrame>();
        state.RequireForUpdate<LocalPlayer>();
        state.RequireForUpdate<RoomsLookup>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var roomsLookup = SystemAPI.GetSingleton<RoomsLookup>();
        var player = SystemAPI.GetSingletonEntity<LocalPlayer>();
        var playerLtw = SystemAPI.GetComponent<LocalToWorld>(player);
        var ecb = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged);

        var refFrame = SystemAPI.GetSingleton<ReferenceFrame>();
        bool playerIsDocked = false;
        foreach (var (ltw, grid, e) in
                 SystemAPI.Query<LocalToWorld, GridStructure>().WithEntityAccess())
        {
            if (!grid.WorldAABB.Contains(playerLtw.Position))
                continue;

            var rooms = roomsLookup.GridRooms.GetValuesForKey(e);
            while (rooms.MoveNext())
            {
                if (!rooms.Current.Item2.BoundingBox.Contains(
                        GridStructure.CellFromPosition(ltw, playerLtw.Position)))
                    continue;

                playerIsDocked = true;
                if (refFrame.Origin != e)
                {
                    ecb.AddComponent(e, new PromoteToReferenceFrame { MakePreviousFrameChildOfNewFrame = true });
                    Debug.Log($"Docking...");
                }
            }
        }

        if (!playerIsDocked && refFrame.Origin != player)
        {
            ecb.AddComponent<PromoteToReferenceFrame>(player);
            Debug.Log($"Undocking...");
        }
    }
}