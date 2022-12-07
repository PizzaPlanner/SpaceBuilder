using Unity.Entities;
using UnityEngine;

namespace Parts
{
    public class ConstructorAuthoring : MonoBehaviour
    {
        public float DelayPerBuilding;
        public float InitialDelayPerItem;
        public float MinDelayPerItem;
    
        class Baker : Baker<ConstructorAuthoring>
        {
            public override void Bake(ConstructorAuthoring authoring)
            {
                throw new System.NotImplementedException();
            }
        }
    }
}