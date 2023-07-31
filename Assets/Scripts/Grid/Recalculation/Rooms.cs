using Grid.Essentials;
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct RoomsLookup : IComponentData, IDisposable
{
    public NativeParallelMultiHashMap<Entity, Cell> Walls;
    public NativeParallelHashMap<(Entity, Cell), int> CellRoomMap;
    public NativeParallelMultiHashMap<Entity, (int, Room)> GridRooms;
    public NativeParallelHashSet<(Entity, Cell)> OccupiedCells;

    public void Initialize()
    {
        Walls = new NativeParallelMultiHashMap<Entity, Cell>(0, Allocator.Persistent);
        CellRoomMap = new NativeParallelHashMap<(Entity, Cell), int>(0, Allocator.Persistent);
        GridRooms = new NativeParallelMultiHashMap<Entity, (int, Room)>(0, Allocator.Persistent);
        OccupiedCells = new NativeParallelHashSet<(Entity, Cell)>(0, Allocator.Persistent);
    }
    
    public void Dispose()
    {
        Walls.Dispose();
        CellRoomMap.Dispose();
        GridRooms.Dispose();
        OccupiedCells.Dispose();
    }
}

public struct Room
{
    public IntAABB BoundingBox;
}