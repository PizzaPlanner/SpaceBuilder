using System;
using Unity.Collections;
using Unity.Mathematics;

public struct SectorLoadState : IDisposable
{
    public NativeQueue<LocalToGalaxy> ToLoad;
    public NativeQueue<LocalToGalaxy> ToUnload;
    NativeParallelMultiHashMap<int3, LocalToGalaxy> _loadedLookup;
    public NativeArray<LocalToGalaxy> Loaded => _loadedLookup.GetValueArray(Allocator.Temp);

    public void Allocate()
    {
        ToLoad = new NativeQueue<LocalToGalaxy>(Allocator.Persistent);
        ToUnload = new NativeQueue<LocalToGalaxy>(Allocator.Persistent);
        _loadedLookup = new (1000, Allocator.Persistent);
    }

    public void Load(LocalToGalaxy ltg)
    {
        _loadedLookup.Add(ltg.LightSector, ltg);
    }

    public void Unload(LocalToGalaxy ltg)
    {
        _loadedLookup.Remove(ltg.LightSector, ltg);
    }

    public void Dispose()
    {
        ToLoad.Dispose();
        ToUnload.Dispose();
        _loadedLookup.Dispose();
    }

    public bool Contains(LocalToGalaxy ltg)
    {
        return _loadedLookup.ContainsKey(ltg.LightSector);
    }

    public void Enqueue(LocalToGalaxy ltg)
    {
        ToLoad.Enqueue(ltg);
    }
        
    public void Dequeue(LocalToGalaxy ltg)
    {
        ToUnload.Enqueue(ltg);
    }
}