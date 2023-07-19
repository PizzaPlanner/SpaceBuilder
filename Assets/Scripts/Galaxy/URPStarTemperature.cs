using Unity.Entities;
using Unity.Rendering;

[MaterialProperty("_Temperature", -1)]
public struct URPStarTemperature : IComponentData, IQueryTypeParameter
{
    public float Value;
}

[MaterialProperty("_FlareIntensity", -1)]
public struct URPStarFlareIntensity : IComponentData, IQueryTypeParameter
{
    public float Value;
}