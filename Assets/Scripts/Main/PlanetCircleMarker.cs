using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

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
                                   radiusWorld * (Data.OrbitRight * math.cos(angle) + Data.OrbitForward * math.sin(angle));
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