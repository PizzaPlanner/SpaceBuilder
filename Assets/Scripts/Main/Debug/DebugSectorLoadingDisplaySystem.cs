using Galaxy;
using Galaxy.SectorLoading;
using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class DebugSectorLoadingDisplaySystem : SystemBase
{
    private VisualElement infoTexts;

    protected override void OnCreate()
    {
        RequireForUpdate<DebugModeSystem.Active>();
    }

    protected override void OnStartRunning()
    {
        infoTexts ??= Object.FindObjectOfType<UIDocument>().rootVisualElement.Q("InfoTexts");
        infoTexts.style.display = DisplayStyle.Flex;
        infoTexts.Q<EnumField>().UnregisterValueChangedCallback(OnMarkerStyleChange);
    }

    void OnMarkerStyleChange(ChangeEvent<Enum> evt)
    {
        var settings = GetSingleton<POIMarker.MarkerSettings>();
        settings.TextStyle = (POIMarker.MarkerSettings.MarkerStyle)evt.newValue;
        SetSingleton(settings);
    }

    protected override void OnStopRunning()
    {
        infoTexts.style.display = DisplayStyle.None;
        infoTexts.Q<EnumField>().RegisterValueChangedCallback(OnMarkerStyleChange);
    }

    protected override void OnUpdate()
    {
        var pendingSectors = SystemAPI.GetSingleton<PendingSectors>();
        var loadedLS = pendingSectors.LightSectors.Loaded.Length;
        var lsToLoad = pendingSectors.LightSectors.ToLoad.Count;
        var lsToUnload = pendingSectors.LightSectors.ToUnload.Count;
        
        var loadedStars = pendingSectors.StarSectors.Loaded.Length;
        var starsToLoad = pendingSectors.StarSectors.ToLoad.Count;
        var starsToUnload = pendingSectors.StarSectors.ToUnload.Count;
        
        var loadedPlanets = pendingSectors.PlanetSectors.Loaded.Length;
        var planetsToLoad = pendingSectors.PlanetSectors.ToLoad.Count;
        var planetsToUnload = pendingSectors.PlanetSectors.ToUnload.Count;

        var stars = GetEntityQuery(typeof(Star)).CalculateEntityCount();
        var planets = GetEntityQuery(typeof(Planet)).CalculateEntityCount();
        var asteroids = GetEntityQuery(typeof(AsteroidData)).CalculateEntityCount();
        var pois = GetEntityQuery(typeof(POIMarkerSystem.MarkedPOI)).CalculateEntityCount();

        var player = SystemAPI.GetSingletonEntity<LocalPlayer>();
        var refFrame = SystemAPI.GetSingleton<ReferenceFrame>();
        var player2 = SystemAPI.GetComponent<Player>(player);
        var ltg = refFrame.CurrentLtg;
        var speed = math.length(refFrame.DeltaLtg) * SystemAPI.Time.DeltaTime;

        var sectorData = GalaxyTools.GetLightSectorData(ltg);
        var starSector = GalaxyTools.GetStarSectorData(ltg);
        var isInStarSector = false;
        var starSectors = sectorData.CalculateStarSectors;
        for (byte i = 0; i < starSectors.Length; i++)
            isInStarSector |= math.all(ltg.StarSector == starSectors[i]);
        var isInPlanetSector = false;
        var planetId = (byte)0;
        if (isInStarSector)
        {
            var planetSectors = starSector.CalculatePlanetSectors;
            for (byte i = 0; i < planetSectors.Length; i++)
            {
                if (math.all(ltg.PlanetSector == planetSectors[i].PlanetSector))
                {
                    isInPlanetSector = true;
                    planetId = i;
                }
            }
            
        }

        var planetSectorInfo = isInPlanetSector ? $" > {starSector.Name} {GalaxyTools.ToRomanNumeral(planetId)} ({GalaxyTools.GetPlanetSectorData(ltg).Type})" : ""; 
        var starSectorInfo = isInStarSector ? $"{starSector.Name} ({starSector.Type}){planetSectorInfo}" : $"{starSector.Name} (Empty)";
        var lightSectorInfo = $"[{GalaxyTools.GetSectorStarFrequency(ltg)}] {sectorData.NumberOfStars} > {starSector.NumberOfPlanets}";
        
        
        infoTexts.Q<Label>("player").text = $@"Sector: {starSectorInfo}
  x: {ltg.LightSector.x:X}:{ltg.StarSector.x:X}:{ltg.PlanetSector.x:X}@{ltg.LocalPosition.x:F0}
  y: {ltg.LightSector.y:X}:{ltg.StarSector.y:X}:{ltg.PlanetSector.y:X}@{ltg.LocalPosition.y:F0} 
  z: {ltg.LightSector.z:X}:{ltg.StarSector.z:X}:{ltg.PlanetSector.z:X}@{ltg.LocalPosition.z:F0}
  {lightSectorInfo}

Speed: {GalaxyTools.ToGalacticDistanceWithUnit(speed)}/{GalaxyTools.ToGalacticDistanceWithUnit(player2.MovementSpeedRun)}
Travel: {speed}";

        infoTexts.Q<Label>("sectors").text = $@"Sectors: {loadedLS} +{lsToLoad} / -{lsToUnload}
Stars: {loadedStars} +{starsToLoad} / -{starsToUnload}
Planets: {loadedPlanets} +{planetsToLoad} / -{planetsToUnload}
";
        
        infoTexts.Q<Label>("things").text = $@"Stars: {stars}
Planets: {planets}
Asteroids: {asteroids}
POIs: {pois}
";
    }
}
