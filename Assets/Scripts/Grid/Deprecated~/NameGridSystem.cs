using Unity.Entities;

public partial class NameGridSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.WithNone<Name>().WithAll<GridStructure>().ForEach((Entity e) =>
            {
                var name = "Grid "+e.GetHashCode();
                EntityManager.AddComponentData(e, new Name {Value = name});
#if UNITY_EDITOR
                EntityManager.SetName(e, name);
#endif
            }).WithStructuralChanges()
            .WithoutBurst()
            .Run();
    }
}
