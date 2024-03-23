using Unity.Mathematics;

public partial struct LocalToGalaxy
{
    public uint LightHash => math.hash(LightSector);
    public uint StarHash => math.hash(StarSector);
    public bool IsEmpty => ((LightHash ^ StarHash) & 0xFFF) > 3500;
}