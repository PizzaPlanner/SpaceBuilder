using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

public class CameraAnchorAuthoring : MonoBehaviour
{
    public float ScrollFactor;
    public float3 MaxOffset;
    
    class Baker : Baker<CameraAnchorAuthoring>
    {
        public override void Bake(CameraAnchorAuthoring authoring)
        {
            AddComponent(new CameraAnchor{MaxOffset = authoring.MaxOffset, ScrollFactor = authoring.ScrollFactor});
        }
    }

    void OnDrawGizmosSelected()
    {
        var minPoint = (float3)transform.position;
        var maxPoint = math.mul(transform.rotation, MaxOffset) + minPoint;
        var midPoint = minPoint + (maxPoint - minPoint) * ScrollFactor;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(minPoint, midPoint);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(maxPoint, midPoint);
    }
}