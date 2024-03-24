using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Grid.Building
{
    [WithNone(typeof(RemoveFromGrid))]
    public partial struct RemovalVisualizationUndoJob : IJobEntity
    {
        public EntityCommandBuffer ecb;

        void Execute(Entity e, ref MaterialMeshInfo materialInfo, in VisualizeRemoval removal)
        {
            if(removal.OriginalMaterialId != default)
                materialInfo.Material = removal.OriginalMaterialId;
            ecb.RemoveComponent<VisualizeRemoval>(e);
        }
    }
    
    [WithChangeFilter(typeof(VisualizeRemoval))]
    [WithNone(typeof(RemoveFromGrid))]
    public partial struct RemovalVisualizationApplyJob : IJobEntity
    {
        public BatchMaterialID RemovalMaterial;
        void Execute(Entity e, ref MaterialMeshInfo mmi, ref VisualizeRemoval removal)
        {
            if(removal.OriginalMaterialId != default)
                return;
            
            removal.OriginalMaterialId = mmi.Material;
            mmi.MaterialID = RemovalMaterial;
        }
    }
}