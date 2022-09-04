using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct BlueprintParserV2 : IBlueprintParser
{
    private const string RInt = @"(-?\d+)";
    private const string RUInt = @"(\d+)";
    private const string RFloat = @"(-?\d*\.?\d+)f";

    public string Save(Entity grid, EntityManager em)
    {
        var name = em.GetComponentData<Name>(grid).Value.Value;

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
                Orientation = new RightAngleRotation(rotation.Value),
                Area = gridItem.Area,
                BuildStatus = em.HasComponent<UnderConstruction>(child.Value) ? em.GetComponentData<UnderConstruction>(child.Value).Progress : uint.MaxValue
            });
        }

        return $@"Name:{name}
Parts:{RInt}\|int3\({RInt}, {RInt}, {RInt}\)\|quaternion\({RFloat}, {RFloat}, {RFloat}, {RFloat}\)\|{RUInt}
{string.Join("\n", parts.Select(x => $"- Part:{x.Id}|{x.Area.Origin}|{x.Orientation.ToString()}|{x.BuildStatus.ToString()}"))}
";
    }

    public BlueprintData Load(string data)
    {
        data = data.Replace("\t", "").Replace("\r", "");
        var lines = data.Split('\n');
        var parts = new List<BuildingPartInfo>();
        var name = "";
        var partsRegex = "<INVALID>";
        foreach (var line in lines)
        {
            if (!line.Contains(":")) continue;
            var idx = line.IndexOf(':');
            var key = line.Substring(0, idx);
            var value = line.Substring(idx + 1).Trim();
            if (key == "Name")
                name = value;
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
                        Id = unchecked((ulong)long.Parse(match.Groups[1].Value)),
                        Area = new BoundingBox(){Origin = new int3(
                            int.Parse(match.Groups[2].Value),
                            int.Parse(match.Groups[3].Value),
                            int.Parse(match.Groups[4].Value))},
                        Orientation = new RightAngleRotation(new quaternion(
                            float.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture),
                            float.Parse(match.Groups[6].Value, CultureInfo.InvariantCulture),
                            float.Parse(match.Groups[7].Value, CultureInfo.InvariantCulture),
                            float.Parse(match.Groups[8].Value, CultureInfo.InvariantCulture))),
                        BuildStatus = uint.Parse(match.Groups[9].Value)
                    };
                    parts.Add(part);
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

        return new BlueprintData {Name = name, Parts = parts};
    }

    public string VersionIdentifier => "Version:2";
}
