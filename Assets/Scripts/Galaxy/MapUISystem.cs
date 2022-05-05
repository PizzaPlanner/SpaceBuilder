using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

public partial class MapUISystem : SystemBase
{
    VisualElement uiRoot;
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
    }

    protected override void OnStartRunning()
    {
        if(uiRoot == null)
            uiRoot = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("MapInfo");
    }

    protected override void OnUpdate()
    {
        Entities
            .WithAll<LocalPlayer>()
            .ForEach((in LocalToWorld ltw, in LocalToGalaxy ltg, in PhysicsVelocity v) =>
            {
                uiRoot.Q<Label>("Sector").text =
                    $"Sector: ({ltg.XInTenKm}, {ltg.YInTenKm})";
                uiRoot.Q<Label>("Coordinates").text =
                    $"Position: ({ltw.Position.x:F1}, {ltw.Position.y:F1}, {ltw.Position.z:F1})";
                uiRoot.Q<Label>("Forward").text =
                    $"Speed: {math.length(v.Linear):F1}m/s";
            }).WithoutBurst().Run();
    }
}
