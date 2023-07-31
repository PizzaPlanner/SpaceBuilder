using Grid.Essentials;
using Unity.Entities;

namespace Grid.Logic
{
    public struct PendingLogicConnection : IBufferElementData
    {
        public byte FromPort;
        public byte ToPort;
        public Cell FromCell;
        public Cell ToCell;
    }
}