using System;
using Unity.Entities;
using Unity.Mathematics;
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
    public Entity Target;
    private const int SIZE = 32;
    public POIMarker()
    {
        AddToClassList("poi-marker");
        style.width = SIZE;
        style.height = SIZE;
        Add(new Label());
    }

    public void Update(EntityManager em, Entity localPlayer, POIMarker.MarkerSettings settings)
    {
        if (!em.Exists(Target) || !em.HasComponent<LocalToGalaxy>(Target))
        {
            RemoveFromHierarchy();
            return;
        }

        var playerLtg = em.GetComponentData<LocalToGalaxy>(localPlayer);
        var playerCts = em.GetComponentData<CanTravelSectors>(localPlayer);
        var targetLtg = em.GetComponentData<LocalToGalaxy>(Target);
        var diffLtg = targetLtg - playerLtg;
        var relativePosition = LocalToGalaxy.RelativePosition(playerLtg, targetLtg);
        var distance = math.length(relativePosition);
        if (distance < 1000)
            relativePosition = em.GetComponentData<WorldTransform>(Target).Position - (float3)Camera.main.transform.position;
        var direction = (float3)math.normalizesafe(relativePosition);



        Vector2 position2D = Camera.main.WorldToScreenPoint((float3)Camera.main.transform.position + direction);
        style.position = Position.Absolute;
        style.left = math.clamp(position2D.x - SIZE / 2, -SIZE / 2, Screen.width + SIZE / 2);
        style.bottom = math.clamp(position2D.y - SIZE / 2, -SIZE / 2, Screen.height + SIZE / 2);
        var dot = math.dot(Camera.main.transform.forward, direction);
        if (dot < 0.3f)
        {
            this.style.opacity = 0;
            return;
        }

        this.style.opacity = math.min(0.9f, math.lerp(0f, 0.9f, math.min(((float)distance - 3) * 0.1f, 10)));

        var name = "<?>";
        var distanceFmt = GalaxyTools.ToGalacticDistanceWithUnit(distance);
        var timeToDistance = distance / playerCts.CurrentSpeed;
        var timeFmt = GalaxyTools.ToTimeUnit(timeToDistance);
        if (em.HasComponent<Name>(Target))
            name = em.GetComponentData<Name>(Target).Value.Value;
        this.Q<Label>().text = settings.TextStyle switch
        {
            MarkerSettings.MarkerStyle.NoText => "",
            MarkerSettings.MarkerStyle.Default => @$"{name}
{distanceFmt}{(playerCts.CurrentSpeed > 0 ? $" // ETA {timeFmt}" : "")}",
            MarkerSettings.MarkerStyle.Debug => @$"{name}
P: {playerLtg.Sector:X}|{playerLtg.SubSector:X}|{playerLtg.LocalPosition}
T: {targetLtg.Sector:X}|{targetLtg.SubSector:X}|{targetLtg.LocalPosition}
D: {diffLtg.Sector:X}|{diffLtg.SubSector:X}|{diffLtg.LocalPosition}
{distanceFmt}",
            _ => ""
        };
    }
}

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class POIMarkerSystem : SystemBase
{
    private VisualElement uiRoot;

    public struct TrackedPOI : IComponentData { }

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
        Entities
            .WithNone<TrackedPOI>()
            .WithAll<LocalToGalaxy>()
            .WithAny<GridStructure, BigObjectInPoiRange>()
            .ForEach((Entity e) =>
            {
                EntityManager.AddComponent<TrackedPOI>(e);
                var hasGrid = HasComponent<GridStructure>(e);
                var isShip = hasGrid && HasComponent<Flyable>(e);

                var type = POIMarker.POIType.Other;
                if (hasGrid)
                    type = isShip ? POIMarker.POIType.Ship : POIMarker.POIType.Station;
                else
                    type = POIMarker.POIType.Star;

                uiRoot.Add(new POIMarker{Target = e, Type = type});
            }).WithoutBurst().WithStructuralChanges().Run();

        var localPlayer = SystemAPI.GetSingletonEntity<LocalPlayer>();
        var markerStyle = SystemAPI.GetSingleton<POIMarker.MarkerSettings>();
        uiRoot.Query<POIMarker>().ForEach(x => x.Update(EntityManager, localPlayer, markerStyle));
    }
}
