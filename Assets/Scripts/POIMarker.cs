using Galaxy;
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

public class POIMarker : VisualElement
{
    
    public struct MarkerSettings : IComponentData
    {
        public enum MarkerStyle
        {
            NoText,
            Default,
            Debug
        }

        public MarkerStyle TextStyle;
    }
    
    public enum POIType
    {
        Ship,
        Player,
        Station,
        Star,
        Other
    }

    public POIType Type;
    public string Name;
    
    private const int SIZE = 32;
    public POIMarker()
    {
        AddToClassList("poi-marker");
        style.width = SIZE;
        style.height = SIZE;
        style.position = Position.Absolute;
        Add(new Label());
    }

    public void Update(LocalToGalaxy deltaLtg, LocalToWorld ltw, CanTravelSectors traveller, MarkerSettings settings)
    {
        var relativePosition = deltaLtg.Distance;
        var distance = math.length(relativePosition);
        var direction = (float3)math.normalizesafe(relativePosition);

        var screenPosition =
            (float2)RuntimePanelUtils.CameraTransformWorldToPanel(this.panel, ltw.Position, Camera.main) - SIZE / 2;
        style.left = screenPosition.x;
        style.top = screenPosition.y;
        var dot = math.dot(Camera.main.transform.forward, direction);
        if (dot < 0.3f)
        {
            this.style.opacity = 0;
            return;
        }

        this.style.opacity = math.min(0.9f, math.lerp(0f, 0.9f, math.min(((float)distance - 3) * 0.1f, 10)));
        
        var distanceFmt = GalaxyTools.ToGalacticDistanceWithUnit(distance);
        var timeToDistance = distance / traveller.CurrentSpeed;
        var timeFmt = GalaxyTools.ToTimeUnit(timeToDistance);
        this.Q<Label>().text = settings.TextStyle switch
        {
            MarkerSettings.MarkerStyle.NoText => "",
            MarkerSettings.MarkerStyle.Default => @$"{Name}
{distanceFmt}{(traveller.CurrentSpeed > 0 ? $" // ETA {timeFmt}" : "")}",
            MarkerSettings.MarkerStyle.Debug => @$"{Name}
Relative: {deltaLtg}
{distanceFmt}",
            _ => ""
        };
    }
}

[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(UpdatePresentationSystemGroup))]
public partial class POIMarkerSystem : SystemBase
{
    private VisualElement uiRoot;

    public class TrackedPOI : ICleanupComponentData
    {
        public POIMarker Marker;
    }

    protected override void OnCreate()
    {
        RequireForUpdate<LocalPlayer>();
        
        var e = EntityManager.CreateEntity(typeof(POIMarker.MarkerSettings));
        SystemAPI.SetComponent(e, new POIMarker.MarkerSettings { TextStyle = POIMarker.MarkerSettings.MarkerStyle.Default });
    }

    protected override void OnStartRunning()
    {
        uiRoot ??= GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("POIMarkers");
    }

    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (ltg, e) in SystemAPI.Query<LocalToGalaxy>()
                                        .WithAll<InPoiRange>()
                                        .WithNone<TrackedPOI>()
                                        .WithEntityAccess())
        {
            var hasGrid = SystemAPI.HasComponent<GridStructure>(e);
            var isShip = hasGrid && SystemAPI.HasComponent<Flyable>(e);

            var type = POIMarker.POIType.Other;
            if (hasGrid)
                type = isShip ? POIMarker.POIType.Ship : POIMarker.POIType.Station;
            else
                type = POIMarker.POIType.Star;

            var name = "<?>";
            if (SystemAPI.HasComponent<Name>(e))
                name = SystemAPI.GetComponent<Name>(e).Value.Value;
            else if (SystemAPI.HasComponent<Star>(e))
                name = GalaxyTools.GetStarSectorData(ltg).Name;

            var poiMarker = new POIMarker { Type = type, Name = name };
            uiRoot.Add(poiMarker);
            ecb.AddComponent(e, new TrackedPOI { Marker = poiMarker });
        }
        foreach(var (trackedPoi, e) in SystemAPI.Query<TrackedPOI>().WithNone<InPoiRange>().WithEntityAccess())
        {
            trackedPoi.Marker.RemoveFromHierarchy();
            ecb.RemoveComponent<TrackedPOI>(e);
        }
        ecb.Playback(EntityManager);
        
        var localPlayer = SystemAPI.GetSingletonEntity<LocalPlayer>();
        var camera = SystemAPI.GetSingletonEntity<CameraAnchor>();
        var cameraTransform = SystemAPI.GetComponent<LocalTransform>(camera);
        var playerLtg = SystemAPI.GetComponent<LocalToGalaxy>(localPlayer);
        playerLtg += cameraTransform.Position;
        var playerCts = SystemAPI.GetComponent<CanTravelSectors>(localPlayer);
        var markerStyle = SystemAPI.GetSingleton<POIMarker.MarkerSettings>();
        foreach(var (trackedPoi, ltg, ltw) in SystemAPI.Query<TrackedPOI, LocalToGalaxy, LocalToWorld>())
        {
            var deltaLtg = ltg - playerLtg;
           trackedPoi.Marker.Update(deltaLtg, ltw, playerCts, markerStyle);
        }
    }
}
