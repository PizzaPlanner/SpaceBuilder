using Unity.Entities;
using UnityEngine;

public class DoorAuthoring : MonoBehaviour
{
    public bool isOpen;
    class Baker : Baker<DoorAuthoring>
    {
        public override void Bake(DoorAuthoring authoring)
        {
            AddComponent(new Door{Open = authoring.isOpen});
        }
    }
}