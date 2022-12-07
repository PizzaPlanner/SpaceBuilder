using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class CameraAnchorAuthoring : MonoBehaviour
{
    public float ScrollFactor;
    public float3 Offset;
    
    class Baker : Baker<CameraAnchorAuthoring>
    {
        public override void Bake(CameraAnchorAuthoring authoring)
        {
            AddComponent(new CameraAnchor{Offset = authoring.Offset, ScrollFactor = authoring.ScrollFactor});
        }
    }
}