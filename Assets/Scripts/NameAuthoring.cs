using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public struct Name : IComponentData
{
    public FixedString32Bytes Value;
}

public class NameAuthoring : MonoBehaviour
{
    public string Name;

    class NameBaker : Baker<NameAuthoring>
    {
        public override void Bake(NameAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.None);
            AddComponent(e, new Name{Value = authoring.Name});
        }
    }
}
