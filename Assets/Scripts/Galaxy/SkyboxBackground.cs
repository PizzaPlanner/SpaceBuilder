using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class SkyboxBackground : MonoBehaviour
{
    public float MoveFactorPerPlanetSector;
}

partial class SkyboxMoveSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<ReferenceFrame>();
    }

    SkyboxBackground _skybox;
    Material _skyboxMat;
    static readonly int Position = Shader.PropertyToID("_Position");

    protected override void OnStartRunning()
    {
        _skybox = GameObject.FindObjectOfType<SkyboxBackground>();
        if (_skybox == null)
        {
            Enabled = false;
            return;
        }
        _skyboxMat = _skybox.GetComponent<MeshRenderer>().material;
    }

    protected override void OnUpdate()
    {
        _skybox.transform.localScale = (float3)Camera.main.farClipPlane * 2;
        var currentPos = SystemAPI.GetSingleton<ReferenceFrame>().CurrentLtg;
        var sector = currentPos.LightSector * LocalToGalaxy.MAX_STARSECTOR * LocalToGalaxy.MAX_PLANETSECTOR 
                     + currentPos.StarSector * LocalToGalaxy.MAX_PLANETSECTOR
                     + currentPos.PlanetSector;
        var posToSkybox = (double3)sector * _skybox.MoveFactorPerPlanetSector;
        _skyboxMat.SetVector(Position, (Vector3)(float3)(posToSkybox % 10000));
    }
}
