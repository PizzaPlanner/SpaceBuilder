using Unity.Entities;
using UnityEngine;

public class SteeringControllerAuthoring : MonoBehaviour
{
    class Baker : Baker<SteeringControllerAuthoring>
    {
        public override void Bake(SteeringControllerAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<SteeringController>(e);
        }
    }
}