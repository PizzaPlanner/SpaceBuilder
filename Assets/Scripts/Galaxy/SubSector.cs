using Unity.Entities;
using Unity.Mathematics;

public struct SubSector : IComponentData { }

public enum SectorType : byte
{
    Empty,
    Star,
    BlackHole,
    Nebula,
}

public enum SubSectorType : byte
{
    Unknown,
    Empty,
    Star,
    Nebula,
    Planet,
    POI,
    AsteroidField,
}

public static class SectorLoadingDistance
{
    public struct CelestialBody : IComponentData { }
    public struct Planetoid : IComponentData { }

}

public struct SectorData : IComponentData
{
    public SectorType Type;
    public uint Seed;
}

public struct SubSectorData : IComponentData
{
    public SubSectorType Type;
    public uint Seed;
}

public struct NebulaSectorData : IComponentData
{
    public int3 Center;
}

public struct StarSectorData : IComponentData
{
    public uint PlanetGeneratorSeed;
    public int3 Center;
    public byte NumberOfPlanets;
}

public struct StarSectorPlanetData : IBufferElementData
{
    public int3 SubSector;
}

public partial class SubSectorGeneratorSystem : SystemBase
{
    private EntityArchetype forceLoadArchetype;

    protected override void OnCreate()
    {
        forceLoadArchetype = EntityManager.CreateArchetype(typeof(ForceLoadSector));
    }

    protected override void OnUpdate()
    {
        var buffer = GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged).AsParallelWriter();
        var forceLoad = forceLoadArchetype;
        Entities
            .WithAll<ToGenerate>()
            .WithAll<SubSector>()
            .WithNone<SectorData>()
            .ForEach((Entity e, int nativeThreadIndex, in LocalToGalaxy ltg) =>
            {
                var sectorData = GalaxyTools.GetSectorData(ltg);
                buffer.AddComponent(nativeThreadIndex, e, sectorData);
                switch (sectorData.Type)
                {
                    case SectorType.Star when math.all(
                                                  ltg.SubSector == GalaxyTools.GetStarSectorData(ltg.Sector).Center):
                    {
                        var starSectorData = GalaxyTools.GetStarSectorData(ltg.Sector);
                        buffer.AddComponent(nativeThreadIndex, e, starSectorData);
                        break;
                    }
                    case SectorType.Star when math.all(ltg.SubSector == 0):
                    {
                        var starSectorData = GalaxyTools.GetStarSectorData(ltg.Sector);
                        var eStarSector = buffer.CreateEntity(nativeThreadIndex, forceLoad);
                        buffer.SetComponent(nativeThreadIndex, eStarSector, new ForceLoadSector{
                            Sector = new LocalToGalaxy
                            {
                                Sector = ltg.Sector,
                                SubSector = starSectorData.Center
                            }});
                        buffer.AddComponent(nativeThreadIndex, e, starSectorData);
                        var planetBuffer = buffer.AddBuffer<StarSectorPlanetData>(nativeThreadIndex, e);
                        for (byte i = 0; i < starSectorData.NumberOfPlanets; i++)
                        {
                            var planetData = GalaxyTools.GetPlanetData(starSectorData, i);
                            var ePlanetSector = buffer.CreateEntity(nativeThreadIndex, forceLoad);
                            buffer.SetComponent(nativeThreadIndex, ePlanetSector, new ForceLoadSector{
                                Sector = new LocalToGalaxy
                                {
                                    Sector = ltg.Sector,
                                    SubSector = planetData.SubSector
                                }});
                            planetBuffer.Add(planetData);
                        }
                        break;
                    }
                    // case SectorType.Nebula when math.all(
                    //     ltg.SubSector == GalaxyTools.GetCelestialSectorCenter(ltg.Sector, out _)):
                    // {
                    //     break;
                    // }
                    case SectorType.Empty:
                        break;
                }

                var subsectorData = GalaxyTools.GetSubSectorData(ltg);
                buffer.AddComponent(nativeThreadIndex, e, subsectorData);
                switch (subsectorData.Type)
                {
                    case SubSectorType.AsteroidField:
                        buffer.AddComponent<GenerateAsteroidField>(nativeThreadIndex, e);
                        break;
                    case SubSectorType.Planet:
                        buffer.AddComponent<GeneratePlanet>(nativeThreadIndex, e);
                        buffer.AddComponent<SectorLoadingDistance.Planetoid>(nativeThreadIndex, e);
                        break;
                    case SubSectorType.POI:
                        buffer.AddComponent<GeneratePOI>(nativeThreadIndex, e);
                        break;
                    case SubSectorType.Star:
                        buffer.AddComponent<GenerateStar>(nativeThreadIndex, e);
                        buffer.AddComponent<SectorLoadingDistance.CelestialBody>(nativeThreadIndex, e);
                        break;
                    case SubSectorType.Nebula:
                        buffer.AddComponent<GenerateNebula>(nativeThreadIndex, e);
                        buffer.AddComponent<SectorLoadingDistance.CelestialBody>(nativeThreadIndex, e);
                        break;
                    case SubSectorType.Empty when sectorData.Type != SectorType.Empty:
                        break;
                }

                buffer.RemoveComponent<ToGenerate>(nativeThreadIndex, e);
            }).ScheduleParallel();
    }
}
