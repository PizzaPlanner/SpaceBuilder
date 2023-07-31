using Grid.Blueprints;
using Grid.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Entities;
using UnityEngine;

public struct BlueprintData
{
    public IEnumerable<string> Tags;
    public IEnumerable<BuildingPartInfo> Parts;
    public IEnumerable<PendingLogicConnection> LogicConnections;
    public string Name;
}

public interface IBlueprintParser
{
    string Save(Entity grid, EntityManager manager);
    public BlueprintData Load(string data);

    public string VersionIdentifier { get; }
}

public static class BlueprintUtils
{
    public const string BlueprintFileExtension = ".yaml";

    private static Dictionary<string, IBlueprintParser> versionToLoader;

    static IBlueprintParser DefaultSerializer => new BlueprintParserV4();

    static void Initialize()
    {
        versionToLoader = new Dictionary<string, IBlueprintParser>();
        foreach (var version in Assembly.GetAssembly(typeof(IBlueprintParser)).GetTypes()
                     .Where(x => !x.IsInterface && !x.IsAbstract && typeof(IBlueprintParser).IsAssignableFrom(x)))
        {
            var item = (IBlueprintParser)Activator.CreateInstance(version);
            if(versionToLoader.ContainsKey(item.VersionIdentifier))
                Debug.LogError($"Blueprint version {item.VersionIdentifier} has multiple parsers defined");
            versionToLoader[item.VersionIdentifier] = item;
        }
    }

    public static void Save(Entity grid) => BlueprintGeneratorSystem.QueueBlueprintSave(grid);

    internal static string Serialize(Entity grid, EntityManager em)
    {
        var parser = DefaultSerializer;
        return $@"{parser.VersionIdentifier}
{parser.Save(grid, em)}";
    }

    public static bool Deserialize(string blueprint, out BlueprintData data)
    {
        if(versionToLoader == null)
            Initialize();
        var versionId = blueprint.Split('\n')[0].Trim();
        if (!versionToLoader.ContainsKey(versionId))
        {
            Debug.LogError($"No Parser found for version identifier '{versionId}'");
            data = default;
            return false;
        }

        try
        {
            data = versionToLoader[versionId].Load(blueprint);
            return data.Name != default;
        }
        catch(Exception e)
        {
            Debug.LogException(e);
            data = default;
            return false;
        }
    }
}
