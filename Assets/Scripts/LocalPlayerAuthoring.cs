using Unity.Entities;
using UnityEngine;

public class LocalPlayerAuthoring : MonoBehaviour
{
    class Baker : Baker<LocalPlayerAuthoring>
    {
        public override void Bake(LocalPlayerAuthoring authoring)
        {
            AddComponent<LocalPlayer>();
        }
    }
}