using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class ThrusterAuthoring : MonoBehaviour
{
    public float MaxStrength;
    
    class ThrusterBaker : Baker<ThrusterAuthoring>
    {
        public override void Bake(ThrusterAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(e, new Thruster
            {
                MaxStrength = authoring.MaxStrength
            });
        }
    }
}