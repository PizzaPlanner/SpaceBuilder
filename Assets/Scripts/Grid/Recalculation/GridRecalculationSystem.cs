using Grid.Blueprints;
using Grid.Logic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Aspects;
using Unity.Transforms;
using UnityEngine;

public struct IntAABB
{
    public int3 Min;
    public int3 Max;

    public int Size => (Max - Min).x * (Max - Min).y * (Max - Min).z;

    public bool Contains(int3 cell) => math.all(cell >= Min) && math.all(cell <= Max);
    
    public static implicit operator IntAABB(BoundingBox bbox) => new IntAABB { Min = bbox.Min, Max = bbox.Max };
}

[UpdateInGroup(typeof(GridRecalculationSystemGroup), OrderFirst = true)]
public partial class GridRecalculationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // TODO: Should this not happen automatically? Maybe scheduling order problem?
        // foreach (var (children, links, e) in
        //          SystemAPI.Query<DynamicBuffer<Child>, DynamicBuffer<LinkedEntityGroup>>()
        //                   .WithChangeFilter<Child>().WithAll<GridStructure>().WithEntityAccess())
        // {
        //     Debug.Log("Recalculating Grid!");
        //     links.Clear();
        //     links.Add(e);
        //     foreach (var child in children)
        //         links.Add(child.Value);
        // }
        
        foreach(var (pending, children) in 
                SystemAPI.Query<PendingBlockSettings, DynamicBuffer<Child>>().WithChangeFilter<Child>())
        {
            if (SystemAPI.HasBuffer<PendingLogicConnection>(pending.Changes))
            {
                var changes = SystemAPI.GetBuffer<PendingLogicConnection>(pending.Changes);
                foreach (var connection in changes)
                {
                    var from = Entity.Null;
                    var to = Entity.Null;
                    foreach (var child in children)
                    {
                        if (!SystemAPI.HasComponent<GridItem>(child.Value))
                            continue;
                        var origin = SystemAPI.GetComponent<GridItem>(child.Value).Area.Origin;
                        if (math.all(origin == connection.FromCell))
                            from = child.Value;
                        else if (math.all(origin == connection.ToCell))
                            to = child.Value;
                    }

                    if (from != Entity.Null && to != Entity.Null && SystemAPI.HasBuffer<LogicPort>(to))
                    {
                        var buffer = SystemAPI.GetBuffer<LogicPort>(to);
                        var port = buffer[connection.ToPort];
                        port.Connection = new LogicInput
                        {
                            FromEntity = from,
                            FromPort = connection.FromPort
                        };
                        buffer[connection.ToPort] = port;
                    }
                    else
                        Debug.LogWarning($"Skipped loading invalid Logic connection");
                }
                changes.Clear(); 
            }
            
            if (SystemAPI.HasBuffer<PendingLogicUIData>(pending.Changes))
            {
                var changes = SystemAPI.GetBuffer<PendingLogicUIData>(pending.Changes);
                foreach (var logicUI in changes)
                {
                    foreach (var child in children)
                    {
                        if (!SystemAPI.HasComponent<GridItem>(child.Value) || !SystemAPI.HasComponent<LogicItem>(child.Value))
                            continue;
                        
                        if(math.any(SystemAPI.GetComponent<GridItem>(child.Value).Area.Origin != logicUI.Target))
                            continue;
                        
                        SystemAPI.SetComponent(child.Value, new LogicItem { SavedUIPosition = logicUI.Position });
                    }
                }
                changes.Clear(); 
            }
               
        }
    }
}
