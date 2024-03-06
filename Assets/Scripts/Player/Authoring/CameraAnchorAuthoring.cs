using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using UnityEngine;

public class CameraAnchorAuthoring : MonoBehaviour
{
    public float ScrollFactor;
    public Transform FarAnchor;
    public Transform CloseAnchor;
    public PhysicsCategoryTags CollidesWith;
    
    class Baker : Baker<CameraAnchorAuthoring>
    {
        public override void Bake(CameraAnchorAuthoring authoring)
        {
            var collisionFilter = CollisionFilter.Default;
            collisionFilter.CollidesWith = authoring.CollidesWith.Value;
            AddComponent(GetEntity(TransformUsageFlags.Dynamic), new CameraSettings
            {
                CloseAnchor = authoring.CloseAnchor.localPosition,
                FarAnchor = authoring.FarAnchor.localPosition,
                ScrollRatio = authoring.ScrollFactor,
                CollidesWith = collisionFilter,
                Orientation = quaternion.identity
            });
        }
    }

    void OnDrawGizmosSelected()
    {
        var minPoint = CloseAnchor.position;
        var maxPoint = FarAnchor.position;
        var midPoint = minPoint + (maxPoint - minPoint) * ScrollFactor;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(minPoint, midPoint);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(maxPoint, midPoint);
    }
}