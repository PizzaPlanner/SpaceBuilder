#if UNITY_EDITOR
using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;

[RequireComponent(typeof(PhysicsShapeAuthoring))]
public class TargetableAuthoring : MonoBehaviour
{
    public bool isInteractable;
    
    [NonSerialized] PhysicsShapeAuthoring _collider;
    private void OnValidate()
    {
        _collider ??= GetComponent<PhysicsShapeAuthoring>();
        var belongsTo = _collider.BelongsTo;
        belongsTo.Category07 = true;
        _collider.BelongsTo = belongsTo;
    }
    class TargetableBaker : Baker<TargetableAuthoring>
    {
        public override void Bake(TargetableAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.None);
            if(authoring.isInteractable)
                AddComponent<Interactable>(e);
        }
    }
}
#endif