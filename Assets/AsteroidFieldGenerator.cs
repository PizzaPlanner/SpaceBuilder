using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[GenerateAuthoringComponent]
public struct AsteroidFieldData : IComponentData
{
    public int3 Bounds;
    [Range(0,5)]
    public float density;
    public Entity prefab;
    public int2 diameterRange;
    public uint seed;
}

public struct ToGenerate : IComponentData { }

public partial class GenerateAsteroidFieldSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;
    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var buffer = bufferSystem.CreateCommandBuffer().AsParallelWriter();
        Entities
            .ForEach((int nativeThreadIndex, in Translation t, in AsteroidFieldData data) =>
            {
                var occupied = new NativeList<int3>(Allocator.Temp);
                var random = new Unity.Mathematics.Random {state = data.seed};

                var dmMax = data.diameterRange.y;

                var tries = 0;
                while (tries++ < 100 && dmMax > data.diameterRange.x)
                {
                    var rPos = random.NextInt3(int3.zero, data.Bounds);
                    var diameter = random.NextInt(data.diameterRange.x, dmMax);
                    var (x, y, z) = (rPos.x, rPos.y, rPos.z);

                    var anyOccupied = false;
                    for (int x1 = math.max(0, x - diameter); x1 < math.min(data.Bounds.x, x + diameter); x1++)
                    for (int y1 = math.max(0, y - diameter); y1 < math.min(data.Bounds.y, y + diameter); y1++)
                    for (int z1 = math.max(0, z - diameter); z1 < math.min(data.Bounds.z, z + diameter); z1++)
                        anyOccupied |= occupied.Contains(new int3(x1, y1, z1));

                    if (anyOccupied)
                    {
                        tries++;
                        dmMax--;
                        continue;
                    }

                    tries -= 2;

                    var e = buffer.Instantiate(nativeThreadIndex, data.prefab);
                    buffer.SetComponent(nativeThreadIndex, e, new AsteroidData
                    {
                        Diameter = diameter,
                        Seed = random.NextFloat3(new float3(2, 2, 2), new float3(8, 8, 8))
                    });
                    buffer.SetComponent(nativeThreadIndex, e, new Translation {Value = t.Value + rPos});
                    buffer.AddComponent(nativeThreadIndex, e, new ToGenerate());

                    var xden = (math.abs(random.NextFloat(0, 5 - data.density)) + 0.5f) * diameter;
                    var yden = (math.abs(random.NextFloat(0, 5 - data.density)) + 0.5f) * diameter;
                    var zden = (math.abs(random.NextFloat(0, 5 - data.density)) + 0.5f) * diameter;
                    for (int x1 = (int) math.max(0, xden); x1 < math.min(data.Bounds.x, xden); x1++)
                    for (int y1 = (int) math.max(0, yden); y1 < math.min(data.Bounds.y, yden); y1++)
                    for (int z1 = (int) math.max(0, zden); z1 < math.min(data.Bounds.z, zden); z1++)
                    {
                        occupied.Add(new int3(x+x1, y+y1, z+z1));
                    }
                }
            }).ScheduleParallel();

        EntityManager.DestroyEntity(GetEntityQuery(typeof(AsteroidFieldData)));
        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
