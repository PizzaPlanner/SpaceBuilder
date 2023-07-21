using Galaxy;
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
            Default,
            Highlighted,
            Debug
        }

        public float FocusStrength;
        public MarkerStyle TextStyle;
    }
    
    public enum POIType
    {
        Ship,
        Player,
        Station,
        Star,
        Planet,
        Other
    }

    public POIType Type
    {
        set
        {
            this.Q("Icon").ClearClassList();
            this.Q("Icon").AddToClassList(value.ToString());
        }
    }
    public string Name;
    
    public POIMarker()
    {
        AddToClassList("poi-marker");
        style.position = Position.Absolute;
        var content = new VisualElement();
        Add(content);
        content.Add(new VisualElement { name = "Icon" });
        content.Add(new Label());
    }

    public void Update(LocalToGalaxy deltaLtg, LocalToWorld ltw, CanTravelSectors traveller, MarkerSettings settings)
    {
        var relativePosition = deltaLtg.Distance;
        var distance = math.length(relativePosition);

        var screenPosition =
            (float2)RuntimePanelUtils.CameraTransformWorldToPanel(this.panel, ltw.Position, Camera.main);
            
        var panelSize = new float2(parent.resolvedStyle.width, parent.resolvedStyle.height);
        if (settings.FocusStrength < 0)
            screenPosition += math.select(panelSize, -panelSize, screenPosition < panelSize / 2);
        style.left = math.clamp(screenPosition.x, 0, panelSize.x);
        style.top  = math.clamp(screenPosition.y, 0, panelSize.y);

        this.style.opacity = math.min(0.9f, math.lerp(0f, 0.9f, math.min(((float)distance - 3) * 0.1f, 10)));
        
        if (settings.FocusStrength > 0.999f || settings.TextStyle is MarkerSettings.MarkerStyle.Highlighted)
        {
            this.AddToClassList("highlighted");
            var distanceFmt = GalaxyTools.ToGalacticDistanceWithUnit(distance);
            var timeToDistance = distance / traveller.CurrentSpeed;
            var timeFmt = GalaxyTools.ToTimeUnit(timeToDistance);
            this.Q<Label>().text = @$"{Name}
{distanceFmt}{(traveller.CurrentSpeed > 0 ? $" // ETA {timeFmt}" : "")}";
        }
        else
            this.RemoveFromClassList("highlighted");
    }
    
    public new class UxmlFactory : UxmlFactory<POIMarker, UxmlTraits> { }
}

[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(UpdatePresentationSystemGroup))]
public partial class POIMarkerSystem : SystemBase
{
    private VisualElement uiRoot;

    public class MarkedPOI : ICleanupComponentData
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
                                        .WithAll<ShouldShowAsPOI>()
                                        .WithNone<MarkedPOI>()
                                        .WithEntityAccess())
        {
            var hasGrid = SystemAPI.HasComponent<GridStructure>(e);
            var isShip = hasGrid && SystemAPI.HasComponent<Flyable>(e);

            var type = POIMarker.POIType.Other;
            if (hasGrid)
                type = isShip ? POIMarker.POIType.Ship : POIMarker.POIType.Station;

            var name = "<?>";
            if (SystemAPI.HasComponent<Star>(e))
            {
                name = GalaxyTools.GetStarSectorData(ltg).Name;
                type = POIMarker.POIType.Star;
            }
            else if (SystemAPI.HasComponent<Planet>(e))
            {
                name = GalaxyTools.GetPlanetSectorData(ltg).Name;
                type = POIMarker.POIType.Planet;
            }
            if (SystemAPI.HasComponent<Name>(e))
                name = SystemAPI.GetComponent<Name>(e).Value.Value;

            var poiMarker = new POIMarker { Type = type, Name = name };
            uiRoot.Add(poiMarker);
            ecb.AddComponent(e, new MarkedPOI { Marker = poiMarker });
        }
        foreach(var (trackedPoi, e) in SystemAPI.Query<MarkedPOI>().WithNone<ShouldShowAsPOI>().WithEntityAccess())
        {
            trackedPoi.Marker.RemoveFromHierarchy();
            ecb.RemoveComponent<MarkedPOI>(e);
        }
        ecb.Playback(EntityManager);
        
        var localPlayer = SystemAPI.GetSingletonEntity<LocalPlayer>();
        var refLtg = SystemAPI.GetSingleton<ReferenceFrame>().CurrentLtg;
        var playerCts = SystemAPI.GetComponent<CanTravelSectors>(localPlayer);
        var markerStyle = SystemAPI.GetSingleton<POIMarker.MarkerSettings>();
        var camForward = (float3)Camera.main.transform.forward;
        foreach(var (trackedPoi, ltg, ltw) in SystemAPI.Query<MarkedPOI, LocalToGalaxy, LocalToWorld>())
        {
            var deltaLtg = ltg - refLtg;
            var inFocus = math.dot(camForward, math.normalizesafe(deltaLtg.Distance));
            var markerSettings = markerStyle;
            markerSettings.FocusStrength = (float)inFocus;
            trackedPoi.Marker.Update(deltaLtg, ltw, playerCts, markerSettings);
        }
    }
}
