using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class ThrusterAuthoring : MonoBehaviour
{
    public float RequiredPower;
    public float Strength;
    public float MaxStrength;
    public float3 ForwardThrust;
    public GridItem.Side DefaultDirection;
    
    class ThrusterBaker : Baker<ThrusterAuthoring>
    {
        public override void Bake(ThrusterAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(e, new Thruster
            {
                On = false,
                Strength = authoring.Strength,
                DefaultDirection = authoring.DefaultDirection,
                ForwardThrust = authoring.ForwardThrust,
                MaxStrength = authoring.MaxStrength,
                RequiredPower = authoring.RequiredPower
            });
        }
    }
}