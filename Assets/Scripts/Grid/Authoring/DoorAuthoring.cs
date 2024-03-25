using System;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class DoorAuthoring : MonoBehaviour
{
    public bool isOpen;
    public GameObject TheDoor;
    public Transform ClosedAnchor;
    public Transform OpenAnchor;

    void OnValidate()
    {
        if(ClosedAnchor == null || OpenAnchor == null || TheDoor == null)
            return;

        if (isOpen)
        {
            TheDoor.transform.position = OpenAnchor.position;
            TheDoor.transform.rotation = OpenAnchor.rotation;
        }
        else
        {
            TheDoor.transform.position = ClosedAnchor.position;
            TheDoor.transform.rotation = ClosedAnchor.rotation;
        }
    }

    class Baker : Baker<DoorAuthoring>
    {
        public override void Bake(DoorAuthoring authoring)
        {
            AddComponent(GetEntity(TransformUsageFlags.None), new Door
            {
                Open = authoring.isOpen,
                TheDoor = GetEntity(authoring.TheDoor, TransformUsageFlags.Dynamic),
                OpenAnchor = LocalTransform.FromMatrix(authoring.OpenAnchor.localToWorldMatrix),
                ClosedAnchor = LocalTransform.FromMatrix(authoring.ClosedAnchor.localToWorldMatrix),
            });
        }
    }
}