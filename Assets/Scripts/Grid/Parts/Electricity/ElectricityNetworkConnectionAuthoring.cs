using Unity.Entities;
using UnityEngine;

public class ElectricityNetworkConnectionAuthoring : MonoBehaviour
{
    public bool IsPort;
    
    class Baker : Baker<ElectricityNetworkConnectionAuthoring>
    {
        public override void Bake(ElectricityNetworkConnectionAuthoring authoring)
        {
            AddComponent(new ElectricityNetworkConnection());
            if (authoring.IsPort)
            {
                AddComponent<ElectricityPort>();
            }
        }
    }
}
