using Unity.Entities;
using UnityEngine;

public class InfiniteElectricityGeneratorAuthoring : MonoBehaviour
{
    class Baker : Baker<InfiniteElectricityGeneratorAuthoring>
    {
        public override void Bake(InfiniteElectricityGeneratorAuthoring authoring)
        {
            AddComponent<InfinityElectricityGenerator>();
        }
    }
}