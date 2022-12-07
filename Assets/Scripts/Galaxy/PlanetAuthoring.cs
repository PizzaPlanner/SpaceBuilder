using Unity.Entities;
using UnityEngine;

public class PlanetAuthoring : MonoBehaviour
{
    class Baker : Baker<PlanetAuthoring>
    {
        public override void Bake(PlanetAuthoring authoring)
        {
            AddComponent<Planet>();
        }
    }
}