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
            AddComponent(new Name{Value = authoring.Name});
        }
    }
}
