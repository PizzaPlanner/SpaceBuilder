using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class ThrusterAuthoring : MonoBehaviour
{
    public Entity Port;
    public float RequiredPower;
    public float Strength;
    public float MaxStrength;
    public float3 ForwardThrust;
    public GridItem.Side DefaultDirection;
    
    class ThrusterBaker : Baker<ThrusterAuthoring>
    {
        public override void Bake(ThrusterAuthoring authoring)
        {
            throw new System.NotImplementedException();
        }
    }
}