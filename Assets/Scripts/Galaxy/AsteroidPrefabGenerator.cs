using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

public class AsteroidPrefabGenerator : MonoBehaviour, IConvertGameObjectToEntity
{
    public int Amount;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        var random = new Unity.Mathematics.Random(7162845);
        var generator = new MarchingCubesSphericalGenerator(100, true);
        for (int i = 0; i < Amount; i++)
        {
            var e = dstManager.Instantiate(entity);
            dstManager.AddComponent<Prefab>(e);
            generator.Generate(random.NextInt(15, 100), random.NextFloat3(new float3(2, 2, 2), new float3(8, 8, 8)));
            var rm = dstManager.GetSharedComponentManaged<RenderMesh>(e);
            rm.mesh = generator.Bake(true);
            dstManager.SetSharedComponentManaged(e, rm);
            dstManager.SetComponentData(e, new RenderBounds{Value = new AABB{Center = rm.mesh.bounds.center, Extents = rm.mesh.bounds.extents }});
            dstManager.SetComponentData(e, new AsteroidData{Seed = random.NextUInt()});
        }
        dstManager.DestroyEntity(entity);
    }
}
