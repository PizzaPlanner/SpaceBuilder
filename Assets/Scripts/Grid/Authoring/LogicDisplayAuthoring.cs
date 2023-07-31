using Grid.Logic;
using Unity.Entities;
using UnityEngine;

public class LogicDisplayAuthoring : MonoBehaviour
{
    class Baker : Baker<LogicDisplayAuthoring>
    {
        public override void Bake(LogicDisplayAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<LogicDisplay>(e);
        }
    }
}