using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;

public class TargetableAuthoring : MonoBehaviour
{
    public bool isInteractable;
    class TargetableBaker : Baker<TargetableAuthoring>
    {
        public override void Bake(TargetableAuthoring authoring)
        {
            AddComponent<Targetable>();
            if(authoring.isInteractable)
                AddComponent<Interactable>();
        }
    }
}