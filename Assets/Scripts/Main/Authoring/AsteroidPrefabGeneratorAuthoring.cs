using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

public class AsteroidPrefabGeneratorAuthoring : MonoBehaviour
{
    public Material Material;
    public Mesh MyMesh;

    void OnValidate()
    {
        var random = new Unity.Mathematics.Random(7162845);
        var generator = new MarchingCubesSphericalGenerator(100, true);
        var data = generator.Generate(random.NextInt(5, 10), random.NextFloat3(new float3(2, 2, 2), new float3(8, 8, 8)));
        MyMesh = new Mesh();
        Mesh.ApplyAndDisposeWritableMeshData(data, MyMesh);
        MyMesh.RecalculateNormals();
        MyMesh.RecalculateBounds();
    }

    class Baker : Baker<AsteroidPrefabGeneratorAuthoring>
    {
        public override void Bake(AsteroidPrefabGeneratorAuthoring authoring)
        {
            var random = new Unity.Mathematics.Random(7162845);
            var generator = new MarchingCubesSphericalGenerator(100, true);
            var e = GetEntity(TransformUsageFlags.Dynamic);
            var data = generator.Generate(random.NextInt(5, 10), random.NextFloat3(new float3(2, 2, 2), new float3(8, 8, 8)));
            var rm = new RenderMesh();
            rm.mesh = new Mesh();
            rm.material = authoring.Material;
            Mesh.ApplyAndDisposeWritableMeshData(data, rm.mesh);
            rm.mesh.RecalculateNormals();
            rm.mesh.RecalculateBounds();
            AddSharedComponentManaged(e, rm);
            AddComponent(e, new RenderBounds{Value = new AABB{Center = rm.mesh.bounds.center, Extents = rm.mesh.bounds.extents }});
            AddComponent(e, new AsteroidData{Seed = random.NextUInt()});
        }
    }
}
