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
    public const string BlueprintFileExtension = ".txt";

    private static Dictionary<string, IBlueprintParser> versionToLoader;

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

    public static string Save(Entity grid, EntityManager em)
    {
        var parser = new BlueprintParserV3();
        return $@"{parser.VersionIdentifier}
{parser.Save(grid, em)}";
    }

    public static bool Load(string blueprint, out BlueprintData data)
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

        data = versionToLoader[versionId].Load(blueprint);
        return true;
    }
}
