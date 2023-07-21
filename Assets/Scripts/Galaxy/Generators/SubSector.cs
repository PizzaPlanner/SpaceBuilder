using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public enum StarSectorType : byte
{
    Empty,
    Star,
    BigStar,
    BlackHole
}

public enum PlanetSectorType : byte
{
    Empty,
    Planet,
    POI,
    AsteroidField,
}

public struct LightSectorData
{
    public byte NumberOfStars;
    public uint StarGenerationSeed;

    public NativeArray<int3> CalculateStarSectors => GalaxyTools.GetStarSectorsOfLightSector(this);
}

public struct StarSectorData : IComponentData
{
    public uint StarGeneratorSeed;
    public byte NumberOfPlanets;
    public StarSectorType Type;

    public NativeArray<int3> CalculatePlanetSectors => GalaxyTools.GetPlanetSectorsOfStarSector(this);
    public string Name => GalaxyTools.GetStarName(StarGeneratorSeed).Value;
}

public struct PlanetSectorData : IComponentData
{
    public uint Seed;
    public PlanetSectorType Type;
    public string Name => GalaxyTools.GetStarName(Seed).Value;
}