using Grid.Essentials;
using Unity.Entities;
using Unity.Mathematics;

namespace Grid.Logic
{
    public struct PendingLogicUIData : IBufferElementData
    {
        public float2 Position;
        public Cell Target;
    }
}