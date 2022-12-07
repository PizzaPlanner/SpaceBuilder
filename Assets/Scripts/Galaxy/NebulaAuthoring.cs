using Unity.Entities;
using UnityEngine;

public class NebulaAuthoring : MonoBehaviour
{
    class Baker : Baker<NebulaAuthoring>
    {
        public override void Bake(NebulaAuthoring authoring)
        {
            AddComponent<Nebula>();
        }
    }
}