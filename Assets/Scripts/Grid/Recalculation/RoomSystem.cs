using Unity.Burst;
using Unity.Entities;

[UpdateInGroup(typeof(GridRecalculationSystemGroup))]
public partial struct RoomSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        var lookup = new RoomsLookup();
        lookup.Initialize();
        
        state.EntityManager.AddComponent<RoomsLookup>(state.SystemHandle);
        state.RequireForUpdate<RoomsLookup>();
        SystemAPI.SetSingleton(lookup);
    }

    public void OnDestroy(ref SystemState state)
    {
        SystemAPI.GetSingleton<RoomsLookup>().Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var roomsLookup = SystemAPI.GetSingletonRW<RoomsLookup>();
        new GridRoomRecalculationJob()
        {
            roomLookup = roomsLookup.ValueRW
        }.Run();
    }
}