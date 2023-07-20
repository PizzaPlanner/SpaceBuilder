using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

public class AsteroidPrefabGeneratorAuthoring : MonoBehaviour
{
    public int Amount;
    
    class Baker : Baker<AsteroidPrefabGeneratorAuthoring>
    {
        public override void Bake(AsteroidPrefabGeneratorAuthoring authoring)
        {
            var random = new Unity.Mathematics.Random(7162845);
            var generator = new MarchingCubesSphericalGenerator(100, true);
            var mainEntity = GetEntity();
            for (int i = 0; i < authoring.Amount; i++)
            {
                var e = CreateAdditionalEntity();
                // var e = Instantiate(mainEntity);
                AddComponent<Prefab>(e);
                generator.Generate(random.NextInt(15, 100), random.NextFloat3(new float3(2, 2, 2), new float3(8, 8, 8)));
                var rm = new RenderMesh();
                rm.mesh = generator.Bake(true);
                AddSharedComponentManaged(e, rm);
                AddComponent(e, new RenderBounds{Value = new AABB{Center = rm.mesh.bounds.center, Extents = rm.mesh.bounds.extents }});
                AddComponent(e, new AsteroidData{Seed = random.NextUInt()});
            }
            // TODO
            // DestroyEntity(entity);
        }
    }
}
