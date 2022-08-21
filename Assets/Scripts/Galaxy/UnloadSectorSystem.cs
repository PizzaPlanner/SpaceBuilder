using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct UnloadCandidate : IComponentData
{
    public float Timeout;
}

[UpdateInGroup(typeof(LateSimulationSystemGroup), OrderLast = true)]
public partial class UnloadSectorSystem : SystemBase
{
    private EntityCommandBufferSystem bufferSystem;
    private const float KEEP_CANDIDATE_FOR_MAX_SECONDS = 1;
    private LocalToGalaxy lastPlayerLtg;

    protected override void OnCreate()
    {
        bufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        RequireSingletonForUpdate<LocalPlayer>();
        lastPlayerLtg = new LocalToGalaxy {Sector = int.MaxValue, SubSector = int.MaxValue};
    }

    protected override void OnUpdate()
    {
        var ecb = bufferSystem.CreateCommandBuffer();
        var deltaTime = Time.DeltaTime;

        var localToGalaxyEntities = GetEntityQuery(typeof(LocalToGalaxy)).ToEntityArray(Allocator.Temp);
        var localToGalaxyBuffer = GetEntityQuery(typeof(LocalToGalaxy)).ToComponentDataArray<LocalToGalaxy>(Allocator.Temp);
        var playerLtg = GetComponent<LocalToGalaxy>(GetSingletonEntity<LocalPlayer>());
        var toRemoveList = new NativeList<Entity>(Allocator.Temp);

        var shouldRecalculateSectors = math.any(playerLtg.Sector != lastPlayerLtg.Sector);
        var shouldRecalculateSubSectors = shouldRecalculateSectors || math.any(playerLtg.SubSector != lastPlayerLtg.SubSector);
        lastPlayerLtg = playerLtg;
        var sectorLoadSettings = GetSingleton<LoadSectorSystem.SectorLoadSettings>();
        if (shouldRecalculateSectors)
        {
            Entities
                .WithNone<UnloadCandidate>()
                .WithNone<ToGenerate>()
                .WithAll<SectorLoadingDistance.CelestialBody>()
                .WithAll<SubSector>()
                .ForEach((Entity e, in LocalToGalaxy ltg) =>
                {
                    if (GalaxyTools.ShouldUnloadSector(playerLtg, ltg,
                            sectorLoadSettings.MaxLoadedSectorDistanceCelestial))
                        ecb.AddComponent<UnloadCandidate>(e);
                }).Schedule();

            Entities
                .WithAll<UnloadCandidate, SectorLoadingDistance.CelestialBody>()
                .ForEach((Entity e, in LocalToGalaxy ltg) =>
                {
                    if(!GalaxyTools.ShouldUnloadSector(playerLtg, ltg, sectorLoadSettings.MaxLoadedSectorDistanceCelestial))
                        ecb.RemoveComponent<UnloadCandidate>(e);
                }).Schedule();

            Entities
                .WithNone<UnloadCandidate>()
                .WithNone<ToGenerate>()
                .WithAll<SectorLoadingDistance.Planetoid>()
                .WithAll<SubSector>()
                .ForEach((Entity e, in LocalToGalaxy ltg) =>
                {
                    if (GalaxyTools.ShouldUnloadSector(playerLtg, ltg,
                            sectorLoadSettings.MaxLoadedSectorDistancePlanetoid))
                        ecb.AddComponent<UnloadCandidate>(e);
                }).Schedule();

            Entities
                .WithAll<UnloadCandidate, SectorLoadingDistance.Planetoid>()
                .ForEach((Entity e, in LocalToGalaxy ltg) =>
                {
                    if(!GalaxyTools.ShouldUnloadSector(playerLtg, ltg, sectorLoadSettings.MaxLoadedSectorDistancePlanetoid))
                        ecb.RemoveComponent<UnloadCandidate>(e);
                }).Schedule();
        }

        if (shouldRecalculateSubSectors)
        {
            Entities
                .WithNone<UnloadCandidate, SectorLoadingDistance.Planetoid, SectorLoadingDistance.CelestialBody>()
                .WithNone<ToGenerate>()
                .ForEach((Entity e, in LocalToGalaxy ltg) =>
                {
                    if (GalaxyTools.ShouldUnloadSector(playerLtg, ltg,
                            sectorLoadSettings.MaxLoadedSectorDistanceDefault))
                        ecb.AddComponent<UnloadCandidate>(e);
                }).Schedule();

            Entities
                .WithAll<UnloadCandidate>()
                .WithNone<SectorLoadingDistance.Planetoid, SectorLoadingDistance.CelestialBody>()
                .ForEach((Entity e, in LocalToGalaxy ltg) =>
                {
                    if(!GalaxyTools.ShouldUnloadSector(playerLtg, ltg, sectorLoadSettings.MaxLoadedSectorDistanceDefault))
                        ecb.RemoveComponent<UnloadCandidate>(e);
                }).Schedule();
        }


        Entities
            .WithAll<LocalToGalaxy, SubSector>()
            .ForEach((Entity e, ref UnloadCandidate candidate) =>
            {
                candidate.Timeout += deltaTime;
                if (candidate.Timeout > KEEP_CANDIDATE_FOR_MAX_SECONDS)
                {
                    var ltg = GetComponent<LocalToGalaxy>(e);
                    if (ltg == playerLtg)
                    {
                        ecb.RemoveComponent<UnloadCandidate>(e);
                        return;
                    }

                    for(int i = 0; i < localToGalaxyEntities.Length; i++)
                        if (localToGalaxyBuffer[i] == ltg)
                            toRemoveList.Add(localToGalaxyEntities[i]);
                }
            }).Run();

        Entities
            .WithNone<Parent>()
            .WithAll<GridItem>()
            .ForEach((Entity e) =>
            {
                toRemoveList.Add(e);
            }).Run();

        EntityManager.DestroyEntity(toRemoveList);
        bufferSystem.AddJobHandleForProducer(Dependency);
    }
}
