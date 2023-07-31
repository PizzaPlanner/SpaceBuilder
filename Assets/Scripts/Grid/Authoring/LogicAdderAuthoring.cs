using Grid.Logic;
using Unity.Entities;
using UnityEngine;

public class LogicAdderAuthoring : MonoBehaviour
{
    class Baker : Baker<LogicAdderAuthoring>
    {
        public override void Bake(LogicAdderAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<LogicAdder>(e);
        }
    }
}