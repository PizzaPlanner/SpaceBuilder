using Unity.Entities;
using UnityEngine;

public class TargetAuthoring : MonoBehaviour
{
    class TargetBaker : Baker<TargetAuthoring>
    {
        public override void Bake(TargetAuthoring authoring)
        {
            AddComponent<Target>();
        }
    }
}