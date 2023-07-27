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
    protected override void OnCreate()
    {
        RequireForUpdate<GridUpdate>();
    }

    protected override void OnUpdate()
    {
        foreach (var (update, grid) in
                 SystemAPI.Query<GridUpdate, RefRW<GridStructure>>())
        {
            Debug.Log("Recalculating Grid!");
            // Update bounds

            // TODO: Should this not happen automatically? Maybe scheduling order problem?
            foreach (var (children, links, e) in
                     SystemAPI.Query<DynamicBuffer<Child>, DynamicBuffer<LinkedEntityGroup>>()
                              .WithChangeFilter<Child>().WithAll<GridStructure>().WithEntityAccess())
            {
                links.Clear();
                links.Add(e);
                foreach (var child in children)
                    links.Add(child.Value);
            }
        }
    }
}
