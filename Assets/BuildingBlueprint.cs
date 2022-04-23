using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct BuildingPartInfo
{
    public long Id;
    public int3 Position;
    public quaternion Orientation;
}

public struct BuildingBlueprint
{
    public int Version;
    public string Name;
    public Grid Grid;
    public BuildingPartInfo[] Parts;

    public override string ToString()
    {
        return $@"Version:{Version}
Name:{Name}
Grid:{Grid.MaxSize}
Parts:
{string.Join("\n", Parts.Select(x => $"- Part:{x.Id},{x.Position},{x.Orientation.ToString("G", CultureInfo.InvariantCulture)}"))}
";
    }
}

public static class BlueprintUtils
{
    public const int Version = 0x1;
    public static BuildingBlueprint Save(Entity grid, EntityManager em)
    {
        var bp = new BuildingBlueprint();
        bp.Version = Version;
        bp.Name = em.GetComponentData<Name>(grid).Value.Value;
        bp.Grid = em.GetComponentData<Grid>(grid);
        var gridLtw = em.GetComponentData<LocalToWorld>(grid);

        var parts = new List<BuildingPartInfo>();
        var children = em.GetBuffer<Child>(grid);
        foreach (var child in children)
        {
            if (!em.HasComponent<GridItem>(child.Value) || em.HasComponent<Ghost>(child.Value))
                continue;
            var gridItem = em.GetComponentData<GridItem>(child.Value);
            var identifieable = em.GetComponentData<Identifieable>(child.Value);
            var rotation = em.GetComponentData<Rotation>(child.Value);
            var translation = em.GetComponentData<Translation>(child.Value);
            parts.Add(new BuildingPartInfo
            {
                Id = identifieable.ItemId,
                Orientation = rotation.Value,
                Position = Grid.CellFromPosition(gridItem.Type, gridLtw, translation.Value)
            });
        }

        bp.Parts = parts.ToArray();
        return bp;
    }

    // public static Entity Load(string blueprint, EntityManager em)
    // {
    //
    // }
}
