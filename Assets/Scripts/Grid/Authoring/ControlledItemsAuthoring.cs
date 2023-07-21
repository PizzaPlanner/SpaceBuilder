using Unity.Entities;
using UnityEngine;

#if UNITY_EDITOR
public class ControlledItemsAuthoring : MonoBehaviour
{
    public GameObject[] Doors;

    class Baker : Baker<ControlledItemsAuthoring>
    {
        public override void Bake(ControlledItemsAuthoring authoring)
        {
        var buffer = AddBuffer<ControlledItems>();
        foreach (var door in authoring.Doors)
            buffer.Add(new ControlledItems {Target = GetEntity(door), Type = ToggleType.Door});

        foreach(var item in buffer)
            if(item.Target == Entity.Null)
                Debug.LogError($"ControlledItems not configured correctly for {authoring.gameObject.name}");
        }
    }
}
#endif