using System.IO;
using System.Linq;
using System.Text;
using Rival;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class BlueprintLibrary : MonoBehaviour
{
    public static string BaseFolder => Path.Combine(Application.persistentDataPath, "blueprints");
    private const string BlueprintFileExtension = ".txt";
    void Awake()
    {
        if (!Directory.Exists(BaseFolder))
            Directory.CreateDirectory(BaseFolder);

#if UNITY_EDITOR
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Application.dataPath, "Blueprints"), $"*{BlueprintFileExtension}",
                     SearchOption.AllDirectories))
        {
            var toPath = Path.Combine(BaseFolder, file.Split('/', '\\').Last());
            if(!File.Exists(toPath))
                File.Copy(file, Path.Combine(BaseFolder, file.Split('/', '\\').Last()));
        }
#endif
        foreach(var file in Directory.EnumerateFiles(BaseFolder, $"*{BlueprintFileExtension}", SearchOption.AllDirectories))
            Debug.Log("Found Blueprint "+file);
    }

    public static Entity Load(string relativePath, EntityManager manager)
    {
        if (!relativePath.EndsWith(BlueprintFileExtension))
            relativePath += BlueprintFileExtension;
        var path = Path.Combine(BaseFolder, relativePath);
        if (!File.Exists(path))
            return Entity.Null;

        if (!BlueprintUtils.Load(File.ReadAllText(path, Encoding.UTF8), out var data))
            return Entity.Null;

        var grid = manager.CreateEntity(typeof(LocalToWorld),
            typeof(Translation), typeof(Rotation), typeof(TrackedTransform),
            typeof(LocalToGalaxy),
            typeof(Grid), typeof(Name));
        manager.AddBuffer<OccupationBuffer>(grid);
        
        manager.SetComponentData(grid, new Name {Value = data.Name});
        manager.SetComponentData(grid, new Grid {MaxSize = Grid.Size.Normal});
        foreach (var part in data.Parts)
        {
            var e = manager.CreateEntity(typeof(PendingPart));
            manager.SetComponentData(e, new PendingPart{Data = part, Grid = grid});
        }
        Debug.Log($"Loaded Blueprint {data.Name}");
        
        return grid;
    }
}
