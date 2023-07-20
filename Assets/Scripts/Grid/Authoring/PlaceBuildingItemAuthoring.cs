#if UNITY_EDITOR
using Unity.Entities;
using UnityEngine;

public class PlaceBuildingItemAuthoring : MonoBehaviour
{
    public BuildingPartAuthoring[] Parts;

    class Baker : Baker<PlaceBuildingItemAuthoring>
    {
        public override void Bake(PlaceBuildingItemAuthoring authoring)
        {
            var buffer = AddBuffer<BuildableBuffer>();
            foreach (var part in authoring.Parts)
                buffer.Add(new BuildableBuffer{ PrefabId = part.GetComponent<IdentifieableAuthoring>().Id });
        }
    }
}
#endif