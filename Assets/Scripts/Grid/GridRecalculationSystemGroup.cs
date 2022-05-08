using Unity.Entities;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class GridRecalculationSystemGroup : ComponentSystemGroup
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireSingletonForUpdate<IdentifiableMappingSystem.Loaded>();
    }
}
