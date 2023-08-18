using Unity.Entities;
using Unity.Mathematics;

namespace Grid.Logic
{
    public struct LogicItem : IComponentData
    {
        public float2 SavedUIPosition;
    }
}