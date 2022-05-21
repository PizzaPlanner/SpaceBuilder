using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct BlueprintParserV3 : IBlueprintParser
{
    public class ItemData
    {
        public ulong Id;
        public ushort Stack;
    }

    public class PartData
    {
        public int3 Position;
        public RightAngleRotation Orientation;
        public ushort Status;
        public List<ItemData> Inventory;
    }

    public class DataStructure
    {
        public int Version;
        public string Name;
        public Dictionary<ulong, List<PartData>> Parts;

        public void Deserialize(string data)
        {
            Stack<object> CurrentToken = new Stack<object>();
            var lines = data.Split('\n');
            var lastIndent = 0;
            for(int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var indent = line.Length - line.TrimStart(' ', '-').Length;
                var kvp = line.Split(new[]{':'}, 2, StringSplitOptions.None);
                var key = kvp.Length >= 2 ? kvp[0].Trim(' ', '-') : null;
                var value = kvp.Length >= 2 ? kvp[1].Trim() : null;
                if (indent < lastIndent)
                {
                    CurrentToken.Pop();
                }
                line = line.Substring(indent);
                if (key == "Version")
                    int.TryParse(value, out Version);
                else if (key == "Name")
                    Name = value;
                else if (key == "Parts")
                {
                    Parts = new Dictionary<ulong, List<PartData>>();
                    CurrentToken.Push(Parts);
                }
                else if (CurrentToken.Peek() is Dictionary<ulong, List<PartData>> dict && key != null)
                {
                    var id = ulong.Parse(key);
                    dict[id] = new List<PartData>();
                    CurrentToken.Push(Parts[id]);
                }
                else if (CurrentToken.Peek() is List<PartData> parts)
                {
                    if(lines[i].Substring(0, indent).Contains("-"))
                        parts.Add(new PartData{Status = ushort.MaxValue});

                    var current = parts.Last();
                    if (key == "Status")
                        current.Status = ushort.Parse(value);
                    else if (key == "LocalToGrid")
                    {
                        var pos = value.Trim(' ', '[', ']').Split(',');
                        if (pos.Length != 4)
                        {
                            Debug.LogError($"Invalid syntax in Line {i}");
                            continue;
                        }
                        current.Position = new int3(int.Parse(pos[0]), int.Parse(pos[1]), int.Parse(pos[2]));
                        current.Orientation =
                            new RightAngleRotation(byte.Parse(pos[3], NumberStyles.AllowHexSpecifier));
                    }
                    else if (key == "Inventory")
                    {
                        current.Inventory = new List<ItemData>();
                        CurrentToken.Push(current.Inventory);
                    }
                }
                else if (CurrentToken.Peek() is List<ItemData> inventory)
                {
                    if(lines[i].Substring(0, indent).Contains("-"))
                        inventory.Add(new ItemData());

                    var current = inventory.Last();
                    if (key == "Id")
                        current.Id = ulong.Parse(value);
                    if (key == "Stack")
                        current.Stack = ushort.Parse(value);
                }

                lastIndent = indent;
            }
        }
    }

    public string Save(Entity grid, EntityManager em)
    {

        var data = new DataStructure();
        data.Parts = new Dictionary<ulong, List<PartData>>();
        data.Name = em.GetComponentData<Name>(grid).Value.Value;
        data.Version = 3;
        var children = em.GetBuffer<Child>(grid);
        foreach (var child in children)
        {
            if (!em.HasComponent<GridItem>(child.Value) || em.HasComponent<Ghost>(child.Value) ||
                em.HasComponent<GridSubItem>(child.Value))
                continue;
            var gridItem = em.GetComponentData<GridItem>(child.Value);
            var identifieable = em.GetComponentData<Identifieable>(child.Value);
            var rotation = em.GetComponentData<Rotation>(child.Value);
            var translation = em.GetComponentData<Translation>(child.Value);

            ItemData[] inventory = null;
            if (em.HasComponent<Inventory>(child.Value))
            {
                var buffer = em.GetBuffer<Inventory>(child.Value);
                inventory = new ItemData[buffer.Length];
                for (int i = 0; i < inventory.Length; i++)
                {
                    var item = buffer[i].Slot.Item;
                    if (item == Entity.Null)
                        continue;

                    var id = em.GetComponentData<Identifieable>(item).ItemId;
                    var stack = em.HasComponent<Stackable>(item)
                        ? em.GetComponentData<Stackable>(item).Size
                        : (ushort) 1u;
                    inventory[i] = new ItemData {Stack = stack, Id = id};
                }
            }

            if (!data.Parts.ContainsKey(identifieable.ItemId))
                data.Parts[identifieable.ItemId] = new List<PartData>();
            data.Parts[identifieable.ItemId].Add(new PartData
            {
                Orientation = new RightAngleRotation(rotation.Value),
                Position = gridItem.Area.Origin,
                Status = em.HasComponent<UnderConstruction>(child.Value)
                    ? em.GetComponentData<UnderConstruction>(child.Value).Progress
                    : ushort.MaxValue,
                Inventory = inventory?.ToList()
            });
        }

        var rawstr = $@"Name: {data.Name}
Parts:
{string.Join("\n", data.Parts.Keys.Select(x => $@"  {x}:
{string.Join("\n", data.Parts[x].Select(y => $@"
  - LocalToGrid: [{y.Position.x},{y.Position.y},{y.Position.z},{y.Orientation}]
    {(y.Status != ushort.MaxValue ? $"Status: {y.Status}" : "")}
{(y.Inventory == null || y.Inventory.Count == 0 ? "" : $@"    Inventory:
{string.Join("\n", y.Inventory.Select(item => item == null ? $@"
    - null" : $@"
    - Id: {item.Id}
      Stack: {item.Stack}"))}
")}
"))}
"))}";
        return string.Join("\n", rawstr.Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    public BlueprintData Load(string data)
    {
        var ds = new DataStructure();
        ds.Deserialize(data);

        var parts = new List<BuildingPartInfo>();
        foreach (var id in ds.Parts.Keys)
        {
            parts.AddRange(ds.Parts[id].Select(item => new BuildingPartInfo {Id = id, Orientation = item.Orientation, Position = item.Position, BuildStatus = item.Status}));
        }
        return new BlueprintData {Name = ds.Name, Parts = parts};
    }

    public string VersionIdentifier => "Version: 3";
}
