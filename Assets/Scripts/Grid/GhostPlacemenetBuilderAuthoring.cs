using Unity.Entities;
using UnityEngine;

public class GhostPlacemenetBuilderAuthoring : MonoBehaviour
{
    class Baker : Baker<GhostPlacemenetBuilderAuthoring>
    {
        public override void Bake(GhostPlacemenetBuilderAuthoring authoring)
        {
            AddComponent(new GhostPlacementBuilder());
        }
    }
}