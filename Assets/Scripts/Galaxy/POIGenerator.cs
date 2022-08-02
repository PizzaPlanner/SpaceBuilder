using Unity.Entities;
using Unity.Transforms;

public struct GeneratePOI : IComponentData { }

public partial class POIGenerator : SystemBase
{
    private EndSimulationEntityCommandBufferSystem bufferSystem;
    private string[] spawnablePOIs;
    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnStartRunning()
    {
        spawnablePOIs = BlueprintLibrary.SpawnablePOIs;
    }

    protected override void OnUpdate()
    {

        Entities
            .WithAll<GeneratePOI, ToGenerate>()
            .ForEach((Entity e, in LocalToGalaxy ltg, in SubSectorData data) =>
            {
                var random = new Unity.Mathematics.Random(data.Seed);
                var numberOfPOIs = random.NextInt(2, 12);
                for (int i = 0; i < numberOfPOIs; i++)
                {
                    var poiStr = spawnablePOIs[random.NextInt(spawnablePOIs.Length)];
                    var poi = EntityManager.CreateEntity(typeof(ExecuteAdminScript), typeof(Translation), typeof(Rotation), typeof(LocalToGalaxy));
                    EntityManager.SetComponentData(poi, new ExecuteAdminScript{Executor = poi, Script = $"spawn \"{poiStr}\""});
                    EntityManager.SetComponentData(poi, new Translation{Value = random.NextFloat3(-3000, 3000)});
                    EntityManager.SetComponentData(poi, new Rotation{Value = random.NextQuaternionRotation()});
                    EntityManager.SetComponentData(poi, ltg);
                }

                EntityManager.RemoveComponent<GeneratePOI>(e);
            }).WithStructuralChanges().WithoutBurst().Run();

        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
