using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct CloseGrids : IBufferElementData, IComparable<CloseGrids>
{
    public Entity Grid;
    public float Distance;

    public int CompareTo(CloseGrids other)
    {
        return Distance.CompareTo(other.Distance);
    }
}

public partial class CalculateCloseGridsSystem : SystemBase
{
    private const float MAX_DISTANCE_SQ = 32000 * 32000;

    private const float UpdateFrequency = 1f;
    private double lastUpdate;

    protected override void OnCreate()
    {
        RequireForUpdate<LocalPlayer>();
    }

    protected override void OnUpdate()
    {
        if (World.Time.ElapsedTime - lastUpdate < UpdateFrequency)
            return;
        lastUpdate = World.Time.ElapsedTime;

        var localPlayer = GetSingletonEntity<LocalPlayer>();
        var playerLtg = GetComponent<LocalToGalaxy>(localPlayer);
        var gridBuffer = GetBuffer<CloseGrids>(localPlayer);

        gridBuffer.Clear();

        NativeList<CloseGrids> closeGrids = new NativeList<CloseGrids>(Allocator.Temp);
        Entities.ForEach((Entity e, in Grid grid, in LocalToGalaxy ltg) =>
        {
            var relPos = (float3)LocalToGalaxy.RelativePosition(playerLtg, ltg);
            var distSq = math.lengthsq(relPos);
            if (distSq < MAX_DISTANCE_SQ)
                return;

            closeGrids.Add(new CloseGrids { Distance = distSq, Grid = e });
        }).Run();

        closeGrids.Sort();
        foreach(var item in closeGrids)
            gridBuffer.Add(item);
    }
}
