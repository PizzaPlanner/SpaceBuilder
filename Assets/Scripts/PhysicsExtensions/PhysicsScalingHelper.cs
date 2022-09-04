using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Physics
{
    public static class PhysicsScalingHelper
    {
        // WARNING: MUST be 'ref ConvexCollider'. See Comment on ConvexCollider struct on why. 
        public static BlobAssetReference<Collider> Scaled(this ref ConvexCollider collider, in float3 scale, in float bevelRadius = 0)
        {
            var indices = new NativeArray<float3>(collider.ConvexHull.NumVertices, Allocator.Temp);
            for (int i = 0; i < indices.Length; i++)
                indices[i] = collider.ConvexHull.Vertices[i] * scale;
            
            var generationParams = ConvexHullGenerationParameters.Default;
            generationParams.BevelRadius = bevelRadius;
            return ConvexCollider.Create(indices, generationParams, collider.Filter, collider.Material);
        }
        
        public static BlobAssetReference<Collider> Scaled(this in BoxCollider collider, in float3 scale)
        {
            var geometry = collider.Geometry;
            geometry.Size *= scale;
            geometry.Center *= scale;
            return BoxCollider.Create(geometry, collider.Filter, collider.Material);
        }
    }
}