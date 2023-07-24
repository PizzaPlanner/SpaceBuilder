using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using Unity.Transforms;
using UnityEngine;

#if UNITY_EDITOR
[RequireComponent(typeof(IdentifieableAuthoring))]
[RequireComponent(typeof(PhysicsShapeAuthoring))]
public class BuildingPartAuthoring : MonoBehaviour
{
    [SerializeField] private uint3 SizeInGrid;
    [SerializeField] private bool Scalable;
    [SerializeField] private bool IsWall;
    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube((float3)SizeInGrid * 0.5f * GridStructure.CELL_SIZE, (float3)SizeInGrid * GridStructure.CELL_SIZE);
    }

    [NonSerialized] PhysicsShapeAuthoring _collider;
    private void OnValidate()
    {
        _collider ??= GetComponent<PhysicsShapeAuthoring>();
        var belongsTo = _collider.BelongsTo;
        if (!math.all(SizeInGrid == 1))
            Scalable = false;

        if (Scalable)
            _collider.ForceUnique = true;
        
        if (IsWall)
            belongsTo.Category08 = true;

        _collider.BelongsTo = belongsTo;
    }

    class Baker : Baker<BuildingPartAuthoring>
    {
        public override void Bake(BuildingPartAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Parent>(entity);
            AddComponent(entity, new GridItem
            {
                Area = new BoundingBox{Origin = int3.zero, Extents = (int3)authoring.SizeInGrid - 1},
                Scalable = math.all(authoring.SizeInGrid == 1) && authoring.Scalable,
                IsWall = authoring.IsWall
            });
            if(authoring.Scalable)
                AddComponent(entity, new PostTransformMatrix
                {
                    Value = float4x4.Translate(GridStructure.CellCenterToLocal(0)) 
                });
        }
    }
}
#endif