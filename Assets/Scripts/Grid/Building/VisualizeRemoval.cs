using Unity.Entities;
using UnityEngine.Rendering;

namespace Grid.Building
{
    public struct VisualizeRemoval : IComponentData
    {
        public int OriginalMaterialId;
    }
}