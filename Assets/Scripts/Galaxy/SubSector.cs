using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public enum LightSectorType : byte
{
    Empty,
    Stars,
}

public enum StarSectorType : byte
{
    Empty,
    Star,
    BlackHole
}

public enum PlanetSectorType : byte
{
    Empty,
    Planet,
    POI,
    AsteroidField,
}

public static class SectorLoadingDistance
{
    public struct CelestialBody : IComponentData { }
    public struct Planetoid : IComponentData { }

}

public struct LightSectorData : IComponentData
{
    public float Perlin;
    public byte NumberOfStars;
    public LightSectorType Type;
    public FixedList128Bytes<ushort> StarSectorBytes;

    public int3 GetStarSector(byte i) => new int3(
        StarSectorBytes[i * 3],
        StarSectorBytes[i * 3 + 1],
        StarSectorBytes[i * 3 + 2]);
}

public struct StarSectorData : IComponentData
{
    public uint StarGeneratorSeed;
    public byte NumberOfPlanets;
    public StarSectorType Type;
    public FixedList128Bytes<ushort> PlanetSectorBytes;

    public int3 GetPlanetSector(byte i) => new int3(
        PlanetSectorBytes[i * 3],
        PlanetSectorBytes[i * 3 + 1],
        PlanetSectorBytes[i * 3 + 2]);
}

public struct PlanetSectorData : IComponentData
{
    public uint Seed;
    public PlanetSectorType Type;
}