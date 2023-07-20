#if UNITY_EDITOR
using Unity.Entities;
using UnityEngine;

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
#endif