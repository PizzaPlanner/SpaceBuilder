using Unity.Entities;

namespace Grid.Blueprints
{
    public struct PendingBlockSettings : IComponentData
    {
        public Entity Changes;
    }
}