using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct CloseGrids : IBufferElementData
{
    public Entity Grid;
    public float Distance;
}

public partial class CalculateCloseGridsSystem : SystemBase
{
    private const float MAX_DISTANCE_SQ = 500f * 500f;

    private const float UpdateFrequency = 1f;
    private double lastUpdate;

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
    }

    protected override void OnUpdate()
    {
        if (Time.ElapsedTime - lastUpdate < UpdateFrequency)
            return;
        lastUpdate = Time.ElapsedTime;

        var localPlayer = GetSingletonEntity<LocalPlayer>();
        var playerPosition = GetComponent<Translation>(localPlayer).Value;
        var gridBuffer = GetBuffer<CloseGrids>(localPlayer);

        gridBuffer.Clear();

        var query = GetEntityQuery(typeof(Translation), typeof(Grid));
        var grids = query.ToEntityArray(Allocator.Temp);
        var list = new List<CloseGrids>();
        foreach (var entity in grids)
        {
            var dist = math.distancesq(GetComponent<Translation>(entity).Value, playerPosition);
            if (dist > MAX_DISTANCE_SQ)
                continue;
            list.Add(new CloseGrids {Grid = entity, Distance = dist});
        }
        list.Sort((x,y) => x.Distance.CompareTo(y.Distance));
        foreach(var item in list)
            gridBuffer.Add(item);
    }
}
