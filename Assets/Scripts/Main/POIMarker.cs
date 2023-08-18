using Galaxy;
using Galaxy.SectorLoading;
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

    POIType _type;
    public POIType Type
    {
        set
        {
            this.Q("Icon").ClearClassList();
            this.Q("Icon").AddToClassList(value.ToString());
            _type = value;
        }
        get => _type;
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

    public void Update(LocalToGalaxy deltaLtg, LocalToWorld ltw, double3 distanceDelta, MarkerSettings settings)
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
            var currentSpeed = math.length(distanceDelta);
            this.AddToClassList("highlighted");
            var distanceFmt = GalaxyTools.ToGalacticDistanceWithUnit(distance);
            var timeToDistance = distance / currentSpeed;
            var timeFmt = GalaxyTools.ToTimeUnit(timeToDistance);
            this.Q<Label>().text = @$"{Name} ({_type})
{distanceFmt}{(currentSpeed > 0 ? $" // ETA {timeFmt}" : "")}";
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
            var type = POIMarker.POIType.Other;

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
            if (type is POIMarker.POIType.Star)
            {
                foreach (var planet in GalaxyTools.GetStarSectorData(ltg).CalculatePlanetSectors)
                    poiMarker.Add(new PlanetCircleMarker { Data = planet });
            }
            uiRoot.Add(poiMarker);
            ecb.AddComponent(e, new MarkedPOI { Marker = poiMarker });
        }
        foreach(var (trackedPoi, e) in SystemAPI.Query<MarkedPOI>().WithNone<ShouldShowAsPOI>().WithEntityAccess())
        {
            trackedPoi.Marker.RemoveFromHierarchy();
            ecb.RemoveComponent<MarkedPOI>(e);
        }
        ecb.Playback(EntityManager);
        
        var refLtg = SystemAPI.GetSingleton<ReferenceFrame>().CurrentLtg;
        var refDelta = SystemAPI.GetSingleton<ReferenceFrame>().DeltaLtg * SystemAPI.Time.DeltaTime;
        var markerStyle = SystemAPI.GetSingleton<POIMarker.MarkerSettings>();
        var camForward = (float3)Camera.main.transform.forward;
        foreach(var (trackedPoi, ltg, ltw) in SystemAPI.Query<MarkedPOI, LocalToGalaxy, LocalToWorld>())
        {
            var deltaLtg = ltg - refLtg;
            var inFocus = math.dot(camForward, math.normalizesafe(deltaLtg.Distance));
            var markerSettings = markerStyle;
            markerSettings.FocusStrength = (float)inFocus;
            trackedPoi.Marker.Update(deltaLtg, ltw, refDelta, markerSettings);
        }

        foreach (var (trackedPoi, ltg, ltw, body) in SystemAPI.Query<MarkedPOI, LocalToGalaxy, LocalToWorld, CelestialBody>())
        {
            if (trackedPoi.Marker.Type != POIMarker.POIType.Star
                || !trackedPoi.Marker.ClassListContains("highlighted")) continue;
            foreach(var planetMarker in trackedPoi.Marker.Query<PlanetCircleMarker>().Build())
                planetMarker.Update(ltw, body.Size);
        }
    }
}

public class PlanetCircleMarker : VisualElement
{
    public GalaxyTools.PlanetSectorOffsetData Data;
    public float3 ScaledDistance;
    public LocalToWorld Center;
    public PlanetCircleMarker()
    {
        generateVisualContent += OnGenerateMesh;
    }

    const int CIRCLE_POINTS = 64;
    void OnGenerateMesh(MeshGenerationContext ctx)
    {
        var cam = Camera.main;
        var painter = ctx.painter2D;
        
        Vector2[] circlePointsPanel = new Vector2[CIRCLE_POINTS];
        var radiusWorld = math.length(ScaledDistance);
        var centerLocal = RuntimePanelUtils.CameraTransformWorldToPanel(panel, Center.Position, cam);
        for (int i = 0; i < CIRCLE_POINTS; i++)
        {
            var angle = Data.Angle + math.PI * 2 * i / CIRCLE_POINTS;
            var circlePointWorld = Center.Position +
                                    radiusWorld * (Data.OrbitRight * math.cos(angle) + Data.OrbitForward * math.sin(angle))
                                    + (float3)GalaxyTools.PlanetSectorCenter / radiusWorld;
            circlePointsPanel[i] = RuntimePanelUtils.CameraTransformWorldToPanel(panel, circlePointWorld, cam) - centerLocal;
        }
        
        painter.strokeColor = Color.Lerp(Color.yellow, Color.blue, Data.Index / 10f);
        painter.lineWidth = .2f;
        painter.BeginPath();
        // painter.MoveTo(float2.zero);
        painter.MoveTo(circlePointsPanel[0]);
        for (int i = 1; i < CIRCLE_POINTS; i++)
            painter.LineTo(circlePointsPanel[i]);
        painter.ClosePath();
        
        painter.Stroke();
    }

    public void Update(LocalToWorld center, float actualBodySize)
    {
        Center = center;
        ScaledDistance = (float3)(Data.Distance / actualBodySize * center.Value.Scale().x);
        MarkDirtyRepaint();
    }
}
