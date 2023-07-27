using Unity.Entities;
using Unity.Rendering;
using UnityEngine.Rendering;

namespace Grid.Building
{
    [WithNone(typeof(RemoveFromGrid))]
    public partial struct RemovalVisualizationUndoJob : IJobEntity
    {
        public EntityCommandBuffer ecb;

        void Execute(Entity e, ref MaterialMeshInfo materialInfo, in VisualizeRemoval removal)
        {
            materialInfo.MaterialID = removal.OriginalMaterialId;
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
            if(removal.OriginalMaterialCopied)
                return;

            removal.OriginalMaterialId = mmi.MaterialID;
            removal.OriginalMaterialCopied = true;
            mmi.MaterialID = RemovalMaterial;
        }
    }
}