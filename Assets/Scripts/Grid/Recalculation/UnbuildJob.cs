using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct RemoveFromGrid : IComponentData { }

[WithAll(typeof(RemoveFromGrid))]
public partial struct UnbuildJob : IJobEntity
{
    public EntityCommandBuffer ecb;
    public RoomsLookup Rooms;

    void Execute(Entity e, in GridItem gridItem, in Parent parent)
    {
        var grid = parent.Value;
        var area = gridItem.Area;
        for (int x = area.Min.x; x <= area.Max.x; x++)
        for (int y = area.Min.y; y <= area.Max.y; y++)
        for (int z = area.Min.z; z <= area.Max.z; z++)
        {
            Rooms.OccupiedCells.Remove((grid, new int3(x, y, z)));
        }
        
        if (gridItem.IsWall)
        {
            if (Rooms.Walls.TryGetFirstValue(grid, out var value, out var iterator))
            {
                if (area.Contains(value))
                    Rooms.Walls.Remove(iterator);
                while (Rooms.Walls.TryGetNextValue(out value, ref iterator))
                {
                    if (area.Contains(value))
                        Rooms.Walls.Remove(iterator);
                }
            }
        }
        
        // TODO: Reduce Mass
        // TODO: Reduce BoundingBox
        
        ecb.AppendToBuffer(grid, new GridUpdate{Area = area, Type = GridUpdate.UpdateType.Remove});
        ecb.DestroyEntity(e);
    }
}