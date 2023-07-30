using Unity.Entities;
using UnityEngine;

public class RCSAuthoring : MonoBehaviour
{
    public float MaxStrength;
    
    class ThrusterBaker : Baker<RCSAuthoring>
    {
        public override void Bake(RCSAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(e, new RCS
            {
                Strength = authoring.MaxStrength
            });
        }
    }
}