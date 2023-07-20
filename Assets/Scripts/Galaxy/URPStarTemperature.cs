using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

[MaterialProperty("_Temperature", -1)]
public struct URPStarTemperature : IComponentData, IQueryTypeParameter
{
    public float Value;
}

[MaterialProperty("_DefaultIntensity", -1)]
public struct URPStarFlareIntensity : IComponentData, IQueryTypeParameter
{
    public float Value;
}

[MaterialProperty("_StarColor", -1)]
public struct URPStarColor : IComponentData, IQueryTypeParameter
{
    public float4 Value;
}