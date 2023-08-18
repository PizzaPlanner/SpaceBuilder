using Grid.Blueprints;
using Grid.Logic;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;

public struct GeneratePOI : IComponentData { }

[System.Flags]
public enum SpawnableTag
{
    None = 0,
    Generatable = 1 << 0

}

public enum SourceType : byte
{
    Internal,
    Local,
    Steam
}

public struct SpawnablePrefab
{
    public FixedString32Bytes Name;
    public SpawnableTag Tags;
    public SourceType Type;
    public Entity Prefab;
}

public partial class BlueprintGeneratorSystem : SystemBase
{
    struct BlueprintSpawnInfo
    {
        public Entity Prefab;
        public LocalToGalaxy LocalToGalaxy;
        public quaternion AbsoluteRotation;
        public bool ShowMarker;
    }
    
    private static NativeParallelHashMap<FixedString64Bytes, SpawnablePrefab> spawnables;
    private NativeList<Entity> generatablePOIs;
    

    static readonly SharedStatic<NativeQueue<BlueprintSpawnInfo>> toSpawn =
        SharedStatic<NativeQueue<BlueprintSpawnInfo>>
            .GetOrCreate<BlueprintSpawnInfo>();

    static NativeQueue<Entity> toSave;

    static string BaseFolder => Path.Combine(Application.persistentDataPath, "blueprints");
    static string[] SpawnablePOIs => Directory
        .EnumerateFiles(BaseFolder, $"*{BlueprintUtils.BlueprintFileExtension}", SearchOption.AllDirectories)
        .Select(x => x.Substring(BaseFolder.Length + 1)).ToArray();

    public static string[] GetAllSpawnables => spawnables.GetKeyArray(Allocator.Temp).Select(x => x.Value).ToArray();

    protected override void OnCreate()
    {
        // Use SceneSection to differentiate between the "Root"-prefab and the generated sub-prefabs,
        // which will have their scenesection removed.
        _prefabQuery = SystemAPI.QueryBuilder().WithAll<GridStructure, BuildingPartInfo, Prefab, SceneSection>()
                                .WithOptions(EntityQueryOptions.IncludePrefab)
                                .Build();
        RequireForUpdate(_prefabQuery);
        RequireForUpdate<ReferenceFrame>();
        RequireForUpdate<GridItemMappingSystem.Loaded>();

        toSave = new NativeQueue<Entity>(Allocator.Persistent);
    }

    void LoadBlueprints()
    {
        spawnables = new(0, Allocator.Persistent);
        generatablePOIs = new NativeList<Entity>(Allocator.Persistent);
        toSpawn.Data = new NativeQueue<BlueprintSpawnInfo>(Allocator.Persistent);
        
        if (!Directory.Exists(BaseFolder))
            Directory.CreateDirectory(BaseFolder);
        
        Addressables.LoadAssetsAsync<TextAsset>("Blueprints", x =>
        {
            LoadAsPrefab(x.text, x.name, SourceType.Internal);
        }).WaitForCompletion();

        foreach (var poiString in SpawnablePOIs)
        {
            var content = File.ReadAllText(Path.Combine(BaseFolder, poiString), Encoding.UTF8);
            var poiStringNoExtension =
                poiString.Substring(0, poiString.Length - BlueprintUtils.BlueprintFileExtension.Length).Replace('\\', '/');
            LoadAsPrefab(content, poiStringNoExtension, SourceType.Local);
        }
        Debug.Log($"Loaded {spawnables.Count()} spawnables:\n- "+string.Join("\n- ", spawnables.GetKeyArray(Allocator.Temp)));
    }

    protected override void OnStartRunning()
    {
        LoadBlueprints();
        RefreshUI();
    }

    void RefreshUI()
    {
        var bpWindow = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q<BlueprintLibraryWindow>();
        bpWindow.Prefabs = spawnables.GetValueArray(Allocator.Temp);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        spawnables.Dispose();
        generatablePOIs.Dispose();
        toSpawn.Data.Dispose();
        toSave.Dispose();
    }

    public static void QueueBlueprintSpawn(FixedString64Bytes path, LocalToGalaxy ltg, quaternion absoluteOrientation)
    {
        if (!spawnables.IsCreated || !spawnables.ContainsKey(path))
        {
            Debug.LogError($"Failed to spawn blueprint: Not found '{path}'");
            return;
        }
        QueueBlueprintSpawn(spawnables[path].Prefab, ltg, absoluteOrientation);
    }

    static ReferenceFrame _lastRefFrame;
    EntityQuery _prefabQuery;

    public static void QueueBlueprintSpawnAtPlayer(FixedString64Bytes path)
    {
        QueueBlueprintSpawn(spawnables[path].Prefab, _lastRefFrame.CurrentLtg, _lastRefFrame.CurrentLtw.Rotation);
    }
    
    public static void QueueBlueprintSpawnAtPlayer(Entity prefab)
    {
        QueueBlueprintSpawn(prefab, _lastRefFrame.CurrentLtg, _lastRefFrame.CurrentLtw.Rotation, true);
    }

    public static void QueueBlueprintSpawn(Entity prefab, LocalToGalaxy ltg, quaternion absoluteOrientation, bool addMarker = false)
    {
        toSpawn.Data.Enqueue(new BlueprintSpawnInfo
        {
            LocalToGalaxy = ltg, 
            Prefab = prefab, 
            AbsoluteRotation = absoluteOrientation,
            ShowMarker = addMarker
        });
    }

    internal static void QueueBlueprintSave(Entity grid)
    {
        toSave.Enqueue(grid);
    }

    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        _lastRefFrame = SystemAPI.GetSingleton<ReferenceFrame>();

        while (toSpawn.Data.TryDequeue(out var spawn))
        {
            var e = ecb.Instantiate(spawn.Prefab);
            ecb.SetComponent(e, spawn.LocalToGalaxy);
            ecb.SetComponent(e, LocalTransform.FromRotation(spawn.AbsoluteRotation));
            if(spawn.ShowMarker)
                ecb.SetComponentEnabled<ShouldShowAsPOI>(e, true);
        }

        var blueprintsUpdated = false;
        while (toSave.TryDequeue(out var grid))
        {
            var text = BlueprintUtils.Serialize(grid, EntityManager);
            var name = SystemAPI.GetComponent<Name>(grid).Value;
            var targetPath = Path.Combine(BaseFolder, "Exported", $"{name}{BlueprintUtils.BlueprintFileExtension}");
            var file = new FileInfo(targetPath);
            file.Directory.Create();
            File.WriteAllText(targetPath, text, Encoding.UTF8);
            Debug.Log($"Exported Blueprint to {targetPath}");
            LoadAsPrefab(text, $"Exported/{name}", SourceType.Local);
            blueprintsUpdated = true;
        }
        if(blueprintsUpdated)
            RefreshUI();
        
        
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
                    QueueBlueprintSpawn(poiPrefab, newLtg, quaternion.identity);
                }
                Debug.Log($"POI GENERATED");
            }).Schedule();

        EntityManager.RemoveComponent<GeneratePOI>(GetEntityQuery(typeof(GeneratePOI)));
    }

    public void LoadAsPrefab(string text, string key, SourceType type)
    {
        key = $"{type}/{key}";


        if (!BlueprintUtils.Deserialize(text, out var data))
        {
            Debug.LogError($"Error loading Blueprint {key}");
            return;
        }
        
        if (spawnables.TryGetValue(key, out var oldPrefab))
        {
            Debug.Log($"Blueprint {key} already exists: Old blueprint is replaced");
            EntityManager.DestroyEntity(oldPrefab.Prefab);
            spawnables.Remove(key);
        }

        var result = new SpawnablePrefab
        {
            Type = type,
            Name = data.Name
        };

        result.Prefab = EntityManager.Instantiate(_prefabQuery.GetSingletonEntity());
        EntityManager.AddComponent<Prefab>(result.Prefab);
        EntityManager.RemoveComponent<SceneSection>(result.Prefab); // For Query purposes only. See _prefabQuery
        EntityManager.SetComponentData(result.Prefab, new Name {Value = result.Name});
        EntityManager.SetComponentData(result.Prefab, LocalTransform.Identity);
#if UNITY_EDITOR
        EntityManager.SetName(result.Prefab, $"Blueprint: {data.Name}");
#endif
        var postLoadPendingChanges = EntityManager.CreateEntity(typeof(Prefab));
        EntityManager.AddComponentData(result.Prefab, new PendingBlockSettings { Changes = postLoadPendingChanges });
        EntityManager.GetBuffer<LinkedEntityGroup>(result.Prefab).Add(postLoadPendingChanges);
        
        if (data.Parts != null)
        {
            var bpBuffer = EntityManager.GetBuffer<BuildingPartInfo>(result.Prefab);
            foreach (var part in data.Parts)
                bpBuffer.Add(part);
        }

        if (data.LogicConnections != null)
        {
            var logicBuffer = EntityManager.AddBuffer<PendingLogicConnection>(postLoadPendingChanges);
            foreach (var connection in data.LogicConnections)
                logicBuffer.Add(connection);
        }
        
        if (data.LogicUIData != null)
        {
            var logicBuffer = EntityManager.AddBuffer<PendingLogicUIData>(postLoadPendingChanges);
            foreach (var connection in data.LogicUIData)
                logicBuffer.Add(connection);
        }
        
        if(data.Tags != null)
            foreach(var tag in data.Tags)
                if (Enum.TryParse<SpawnableTag>(tag, out var t1))
                    result.Tags |= t1;

        spawnables.Add(key, result);
        if((result.Tags & SpawnableTag.Generatable) != 0)
            generatablePOIs.Add(result.Prefab);
    }
}
