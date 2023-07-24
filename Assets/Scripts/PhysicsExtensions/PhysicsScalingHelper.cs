using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Physics
{
    // TODO: Make sure created blobassets are released properly!
    // See line 35+ in https://github.com/Unity-Technologies/EntityComponentSystemSamples/blob/master/PhysicsSamples/Assets/9.%20Modify/Box%20Collider%20Size/BoxColliderSizeSystem.cs
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
            return ConvexCollider.Create(indices, generationParams, collider.GetCollisionFilter(), collider.Material);
        }
        
        public static BlobAssetReference<Collider> Scaled(this in BoxCollider collider, in float3 scale)
        {
            var geometry = collider.Geometry;
            geometry.Size *= scale;
            geometry.Center *= scale;
            return BoxCollider.Create(geometry, collider.GetCollisionFilter(), collider.Material);
        }
    }
}