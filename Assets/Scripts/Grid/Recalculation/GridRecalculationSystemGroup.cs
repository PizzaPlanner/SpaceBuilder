using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class GridRecalculationSystemGroup : ComponentSystemGroup
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<IdentifiableMappingSystem.Loaded>();
    }

    protected override void OnUpdate()
    {
        base.OnUpdate();
        EntityManager.SetComponentEnabled<GridUpdate>(GetEntityQuery(typeof(GridUpdate)), false);
    }
}
