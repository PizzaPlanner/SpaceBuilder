using Galaxy;
using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

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
        infoTexts ??= GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("InfoTexts");
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
        var pendingSectors = SystemAPI.GetSingleton<LoadSectorSystem.PendingSectors>();
        var loadedSectors = pendingSectors.sectorsLoaded.Length;
        var toLoad = pendingSectors.sectorsToLoad.Count;
        var toUnload = pendingSectors.sectorsToUnload.Count;

        var stars = GetEntityQuery(typeof(Star)).CalculateEntityCount();
        var planets = GetEntityQuery(typeof(Planet)).CalculateEntityCount();
        var asteroids = GetEntityQuery(typeof(AsteroidData)).CalculateEntityCount();
        var pois = GetEntityQuery(typeof(GridStructure)).CalculateEntityCount();

        var player = SystemAPI.GetSingletonEntity<LocalPlayer>();
        var speed = SystemAPI.GetComponent<CanTravelSectors>(player);
        var player2 = SystemAPI.GetComponent<Player>(player);
        var ltg = SystemAPI.GetComponent<LocalToGalaxy>(player);
        
        infoTexts.Q<Label>("player").text = $@"Sector: 
  x: {ltg.Sector.x:X}:{ltg.SubSector.x:X}@{ltg.LocalPosition.x}
  y: {ltg.Sector.y:X}:{ltg.SubSector.y:X}@{ltg.LocalPosition.y} 
  z: {ltg.Sector.z:X}:{ltg.SubSector.z:X}@{ltg.LocalPosition.z}
  {GalaxyTools.GetSectorType(ltg.Sector)} > {GalaxyTools.GetSubSectorData(ltg).Type}
Current Speed: {GalaxyTools.ToGalacticDistanceWithUnit(speed.CurrentSpeed)}
Running Speed: {GalaxyTools.ToGalacticDistanceWithUnit(player2.MovementSpeedRun)}";

        infoTexts.Q<Label>("sectors").text = $@"Loaded: {loadedSectors}
Pending: +{toLoad} / -{toUnload}
";
        
        infoTexts.Q<Label>("things").text = $@"Stars: {stars}
Planets: {planets}
Asteroids: {asteroids}
POIs: {pois}
";
    }
}
