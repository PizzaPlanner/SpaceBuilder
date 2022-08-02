using Unity.Entities;
using UnityEngine;

[GenerateAuthoringComponent]
public struct Planet : IComponentData
{
    public Color Color;
    public float Size;
}
