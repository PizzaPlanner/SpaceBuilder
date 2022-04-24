using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct BuildingPartInfo
{
    public long Id;
    public int3 Position;
    public quaternion Orientation;
}

public struct PendingPart : IComponentData
{
    public Entity Grid;
    public BuildingPartInfo Data;
}

public struct BuildingBlueprint
{
    private const string RInt = @"(-?\d+)";
    private const string RFloat = @"(-?\d*\.?\d+)f";
    public int Version;
    public string Name;
    public Grid Grid;
    public List<BuildingPartInfo> Parts;

    public override string ToString()
    {
        return $@"Version:{Version}
Name:{Name}
Parts:{RInt}\|int3\({RInt}, {RInt}, {RInt}\)\|quaternion\({RFloat}, {RFloat}, {RFloat}, {RFloat}\)
{string.Join("\n", Parts.Select(x => $"- Part:{x.Id}|{x.Position}|{x.Orientation.ToString("G", CultureInfo.InvariantCulture)}"))}
";
    }
}

public static class BlueprintUtils
{
    public const int Version = 0x2;
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
            if (!em.HasComponent<GridItem>(child.Value) || em.HasComponent<Ghost>(child.Value) || em.HasComponent<GridSubItem>(child.Value))
                continue;
            var gridItem = em.GetComponentData<GridItem>(child.Value);
            var identifieable = em.GetComponentData<Identifieable>(child.Value);
            var rotation = em.GetComponentData<Rotation>(child.Value);
            var translation = em.GetComponentData<Translation>(child.Value);
            parts.Add(new BuildingPartInfo
            {
                Id = identifieable.ItemId,
                Orientation = rotation.Value,
                Position = gridItem.Area.Origin
            });
        }

        bp.Parts = parts;
        return bp;
    }

    public static BuildingBlueprint Load(string blueprint)
    {
        blueprint = blueprint.Replace("\t", "").Replace("\r", "");
        var lines = blueprint.Split('\n');
        var compiled = new BuildingBlueprint();
        compiled.Parts = new List<BuildingPartInfo>();
        Parse(ref compiled, lines);
        return compiled;
    }

    private static void Parse(ref BuildingBlueprint blueprint, string[] lines)
    {
        var partsRegex = "<INVALID>";
        foreach (var line in lines)
        {
            if (!line.Contains(":")) continue;
            var idx = line.IndexOf(':');
            var key = line.Substring(0, idx);
            var value = line.Substring(idx + 1).Trim();
            if (key == "Name")
                blueprint.Name = value;
            // else if (key == "Version")
            //     blueprint.Version = int.Parse(value);
            else if (key == "Parts")
                partsRegex = value;
            else if (key == "- Part")
            {
                var match = Regex.Match(value, partsRegex);
                if (!match.Success)
                {
                    Debug.LogError($"Invalid Part Description: {value}");
                    continue;
                }
                try
                {
                    var part = new BuildingPartInfo
                    {
                        Id = long.Parse(match.Groups[1].Value),
                        Position = new int3(
                            int.Parse(match.Groups[2].Value),
                            int.Parse(match.Groups[3].Value),
                            int.Parse(match.Groups[4].Value)),
                        Orientation = new quaternion(
                            float.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture),
                            float.Parse(match.Groups[6].Value, CultureInfo.InvariantCulture),
                            float.Parse(match.Groups[7].Value, CultureInfo.InvariantCulture),
                            float.Parse(match.Groups[8].Value, CultureInfo.InvariantCulture))
                    };
                    blueprint.Parts.Add(part);
                }
                catch
                {
                    var error = new List<string>();
                    error.Add($"Could not Parse Part {value}");
                    for(int i = 0; i < match.Groups.Count; i++)
                        error.Add($"Match {i}: {match.Groups[i].Value}");
                    Debug.LogError(string.Join("\n", error));
                }
            }
        }
    }
}
