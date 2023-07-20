using System;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public struct GeneratePOI : IComponentData { }

[System.Flags]
public enum SpawnableTag
{
    Generatable = 1 << 0

}

public partial class POIGenerator : SystemBase
{
    private static NativeParallelHashMap<Entity, SpawnableTag> spawnableTags;
    private static NativeParallelHashMap<FixedString64Bytes, Entity> spawnables;
    private NativeList<Entity> generatablePOIs;

    static string BaseFolder => Path.Combine(Application.persistentDataPath, "blueprints");
    static string[] SpawnablePOIs => Directory
        .EnumerateFiles(BaseFolder, $"*{BlueprintUtils.BlueprintFileExtension}", SearchOption.AllDirectories)
        .Select(x => x.Substring(BaseFolder.Length + 1)).ToArray();

    public static string[] GetAllSpawnables => spawnables.GetKeyArray(Allocator.Temp).Select(x => x.Value).ToArray();

    protected override void OnCreate()
    {
        if (!Directory.Exists(BaseFolder))
            Directory.CreateDirectory(BaseFolder);

#if UNITY_EDITOR
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Application.dataPath, "Blueprints"), $"*{BlueprintUtils.BlueprintFileExtension}",
                     SearchOption.AllDirectories))
        {
            var relativeFile = file.Substring(Path.Combine(Application.dataPath, "Blueprints").Length + 1);
            var toPath = Path.Combine(BaseFolder, relativeFile);
            File.Copy(file, toPath, true);
        }
#endif
        spawnables = new(SpawnablePOIs.Length, Allocator.Persistent);
        spawnableTags = new (SpawnablePOIs.Length, Allocator.Persistent);
        foreach (var poiString in SpawnablePOIs)
        {
            var prefab = LoadAsPrefab(poiString, EntityManager, out var tags);
            var poiStringNoExtension =
                poiString.Substring(0, poiString.Length - BlueprintUtils.BlueprintFileExtension.Length).Replace('\\', '/');
            if (prefab != Entity.Null)
            {
                spawnables.Add(poiStringNoExtension, prefab);
                spawnableTags.Add(prefab, tags);
            } else
                Debug.LogError($"Loading spawnable {poiStringNoExtension} failed");
        }
        Debug.Log($"Loaded {spawnables.Count()} spawnabled:\n- "+string.Join("\n- ", spawnables.GetKeyArray(Allocator.Temp)));

        generatablePOIs = new NativeList<Entity>(Allocator.Persistent);
        foreach(var thing in spawnableTags)
            if((thing.Value & SpawnableTag.Generatable) != 0)
                generatablePOIs.Add(thing.Key);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        spawnables.Dispose();
        spawnableTags.Dispose();
        generatablePOIs.Dispose();
    }

    public static void QueuePOISpawn(EntityCommandBuffer ecb, FixedString64Bytes path, LocalToGalaxy ltg, LocalTransform transform)
    {
        if (!spawnables.IsCreated || !spawnables.ContainsKey(path))
        {
            Debug.LogError($"There is no spawnable associated with '{path}'");
            return;
        }
        QueuePOISpawn(ecb, spawnables[path], ltg, transform);
    }

    public static void QueuePOISpawn(EntityCommandBuffer ecb, Entity prefab, LocalToGalaxy ltg, LocalTransform transform)
    {
        var e = ecb.Instantiate(prefab);
        ecb.SetComponent(e, ltg);
        ecb.SetComponent(e, transform);
    }

    protected override void OnUpdate()
    {
        var ecb = GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        var keys = generatablePOIs;
        Entities
            .WithAll<GeneratePOI>()
            .ForEach((Entity e, in LocalToGalaxy ltg, in PlanetSectorData data) =>
            {
                var random = new Unity.Mathematics.Random(data.Seed);
                var numberOfPOIs = random.NextInt(2, 12);
                for (int i = 0; i < numberOfPOIs; i++)
                {
                    var poiPrefab = keys[random.NextInt(keys.Length)];
                    var newLtg = ltg;
                    newLtg.LocalPosition = random.NextFloat3(LocalToGalaxy.MAX_POSITION);
                    QueuePOISpawn(ecb, poiPrefab, newLtg, LocalTransform.Identity);
                }
            }).Schedule();

        EntityManager.RemoveComponent<GeneratePOI>(GetEntityQuery(typeof(GeneratePOI)));
    }

    public static Entity LoadAsPrefab(string relativePath, EntityManager manager, out SpawnableTag tags)
    {
        if (!relativePath.EndsWith(BlueprintUtils.BlueprintFileExtension))
            relativePath += BlueprintUtils.BlueprintFileExtension;
        var path = Path.Combine(BaseFolder, relativePath);

        tags = 0;

        if (!File.Exists(path))
            return Entity.Null;

        if (!BlueprintUtils.Load(File.ReadAllText(path, Encoding.UTF8), out var data))
            return Entity.Null;

        var prefab = manager.CreateEntity(typeof(LocalToWorld),
            typeof(LocalTransform),
            typeof(LocalToGalaxy),
            typeof(BuildingPartInfo),
            typeof(CellMap),
            typeof(Rooms),
            typeof(GridStructure), 
            typeof(Name), 
            typeof(Prefab));
        manager.SetComponentData(prefab, new Name {Value = data.Name});
        manager.SetComponentData(prefab, LocalTransform.Identity);
        var bpBuffer = manager.GetBuffer<BuildingPartInfo>(prefab);
        foreach (var part in data.Parts)
        {
            bpBuffer.Add(part);
        }
        if(data.Tags != null)
            foreach(var tag in data.Tags)
                if (Enum.TryParse<SpawnableTag>(tag, out var t1))
                    tags |= t1;

        return prefab;
    }
}
