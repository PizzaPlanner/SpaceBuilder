using Galaxy;
using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class DebugSectorLoadingDisplaySystem : SystemBase
{
    private VisualElement infoTexts;
    VisualElement addSpeed;
    
    protected override void OnCreate()
    {
        RequireForUpdate<DebugModeSystem.Active>();
    }

    protected override void OnStartRunning()
    {
        infoTexts ??= GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("InfoTexts");
        var player = SystemAPI.GetSingletonEntity<LocalPlayer>();
        if (addSpeed == null)
        {
            addSpeed = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("MoreSpeed");
            addSpeed.RegisterCallback<MouseDownEvent>(x =>
            {
                var cts = EntityManager.GetComponentData<Player>(player);
                if (x.button == 0)
                    cts.MovementSpeedRun *= 10;
                else
                    cts.MovementSpeedRun /= 10;
                EntityManager.SetComponentData(player, cts);
            });
        }
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
        var pois = GetEntityQuery(typeof(GridStructure)).CalculateEntityCount();

        var player = SystemAPI.GetSingletonEntity<LocalPlayer>();
        var speed = SystemAPI.GetComponent<CanTravelSectors>(player);
        var player2 = SystemAPI.GetComponent<Player>(player);
        var ltg = SystemAPI.GetComponent<LocalToGalaxy>(player);

        var sectorData = GalaxyTools.GetLightSectorData(ltg);
        var isInStarSector = false;
        var starSectors = sectorData.CalculateStarSectors;
        for (byte i = 0; i < sectorData.NumberOfStars; i++)
            isInStarSector |= math.all(ltg.StarSector == starSectors[i]);
        var starSectorInfo = sectorData.Type switch
        {
            LightSectorType.Empty => $"[{sectorData.Perlin}] Empty",
            LightSectorType.Stars => $"[{sectorData.Perlin}] {sectorData.NumberOfStars}⭐ > {(isInStarSector ? "Solar" : "Empty")}"
        };
        
        infoTexts.Q<Label>("player").text = $@"Sector: 
  x: {ltg.LightSector.x:X}:{ltg.StarSector.x:X}:{ltg.PlanetSector.x:X}@{ltg.LocalPosition.x:F0}
  y: {ltg.LightSector.y:X}:{ltg.StarSector.y:X}:{ltg.PlanetSector.y:X}@{ltg.LocalPosition.y:F0} 
  z: {ltg.LightSector.z:X}:{ltg.StarSector.z:X}:{ltg.PlanetSector.z:X}@{ltg.LocalPosition.z:F0}
  {starSectorInfo}

Speed: {GalaxyTools.ToGalacticDistanceWithUnit(speed.CurrentSpeed)}/{GalaxyTools.ToGalacticDistanceWithUnit(player2.MovementSpeedRun)}
Travel: {speed.CurrentTravelType}";

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
