using Unity.Entities;

namespace Parts {
    public struct Constructor : IComponentData
    {
        public float DelayPerBuilding;
        public float InitialDelayPerItem;
        public float MinDelayPerItem;
        public float TimeSpentOnItem;
        public double LastTick;
    }
}