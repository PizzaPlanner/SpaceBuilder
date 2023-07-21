using Unity.Entities;
using UnityEngine;

public class ConvertToPrefabAuthoring : MonoBehaviour
{
    class Baker  : Baker<ConvertToPrefabAuthoring>
    {
        public override void Bake(ConvertToPrefabAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.None);
            AddComponent<Prefab>(e);
        }
    }
}
