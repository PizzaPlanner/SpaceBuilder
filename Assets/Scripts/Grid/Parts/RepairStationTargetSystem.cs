using Parts;
using Unity.Collections;
using Unity.Entities;

public partial class RepairStationTargetSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var gridQuery = GetEntityQuery(typeof(GridItem), typeof(UnderConstruction));
        var entities = gridQuery.ToEntityArray(Allocator.TempJob);
        var grids = gridQuery.ToComponentDataArray<GridItem>(Allocator.TempJob);
        Entities
            .WithDisposeOnCompletion(entities)
            .WithDisposeOnCompletion(grids)
            .WithNone<UnderConstruction>()
            .WithAll<Constructor>()
            .ForEach((ref Target target, in GridItem item) =>
            {
                target.PreviousValue = target.Value;
                target.Value = Entity.Null;
                for(int i = 0; i < entities.Length && target.Value == Entity.Null; i++)
                    if (grids[i].Grid == item.Grid)
                    {
                        target.Value = entities[i];
                        target.CanConstruct = true;
                    }
            }).Schedule();
    }
}