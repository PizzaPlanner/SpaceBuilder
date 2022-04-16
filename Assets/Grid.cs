using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[GenerateAuthoringComponent]
public struct Grid : IComponentData
{
    public float CellSize;
    public int3 Size;

    public int3 CellFromPosition(LocalToWorld grid, float3 position) => (int3) math.floor(math.mul(math.inverse(grid.Rotation), position - grid.Position) / CellSize);

    public float3 PositionFromCell(LocalToWorld grid, int3 cell) =>  math.mul(grid.Rotation, (float3) cell * CellSize) + grid.Position;
}

public struct Initialized : IComponentData { }

public struct Name : IComponentData
{
    public FixedString32 Value;
}

public class NameGridSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.WithNone<Name>().WithAll<Grid>().ForEach((Entity e) =>
        {
            var name = "Grid "+e.GetHashCode();
            EntityManager.AddComponentData(e, new Name {Value = name});
            #if UNITY_EDITOR
            EntityManager.SetName(e, name);
            #endif
        }).WithStructuralChanges()
            .WithoutBurst()
            .Run();
    }
}
