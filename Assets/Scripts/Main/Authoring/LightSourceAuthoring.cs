using Unity.Entities;
using UnityEngine;

public class LightSourceAuthoring : MonoBehaviour
{
    public Entity Port;
    public float RequiredPower;
    public Entity Light;
    
    class Baker : Baker<LightSourceAuthoring>
    {
        public override void Bake(LightSourceAuthoring authoring)
        {
            AddComponent(new LightSource
            {
                Light = authoring.Light, 
                Port = authoring.Port, 
                RequiredPower = authoring.RequiredPower
            });
        }
    }
}