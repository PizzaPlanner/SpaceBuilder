using Unity.Collections;
using Unity.Entities;
using Unity.Physics;

[GenerateAuthoringComponent]
public struct GridMass : IComponentData
{
    public float Value;
}

[UpdateInGroup(typeof(GridRecalculationSystemGroup))]
public partial class ApplyMassSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate(GetEntityQuery(typeof(GridNeedsUpdate)));
    }

    protected override void OnUpdate()
    {
        var grids = GetEntityQuery(typeof(Grid)).ToEntityArray(Allocator.Temp);
        NativeParallelHashMap<Entity, float> gridMasses = new NativeParallelHashMap<Entity, float>(grids.Length, Allocator.Temp);
        foreach(var item in grids)
            gridMasses.Add(item, 0);
        Entities
            .ForEach((in GridMass mass, in GridItem item) =>
            {
                gridMasses[item.Grid] += mass.Value;
            }).Run();

        Entities.WithAll<Grid>()
            .WithDisposeOnCompletion(gridMasses)
            .ForEach((Entity e, ref PhysicsMass mass) =>
            {
                mass.InverseMass = 1 / gridMasses[e];
            }).Run();
    }
}
