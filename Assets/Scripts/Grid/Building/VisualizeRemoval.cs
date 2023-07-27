using Unity.Entities;
using UnityEngine.Rendering;

namespace Grid.Building
{
    public struct VisualizeRemoval : IComponentData
    {
        public BatchMaterialID OriginalMaterialId;
        internal bool OriginalMaterialCopied;
    }
}