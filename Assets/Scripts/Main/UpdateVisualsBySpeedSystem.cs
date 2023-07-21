using Galaxy.SectorLoading;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class UpdateVisualsBySpeedSystem : SystemBase
{
    private Volume Volume;

    private float targetWeight;

    protected override void OnCreate()
    {
        RequireForUpdate<CameraAnchor>();
        RequireForUpdate<LocalPlayer>();
    }

    protected override void OnStartRunning()
    {
        Volume ??= GameObject.FindObjectsOfType<Volume>().First(x => x.weight == 0);
    }

    protected override void OnUpdate()
    {
        var localPlayer = GetSingletonEntity<LocalPlayer>();
        var speed = GetComponent<CanTravelSectors>(localPlayer).CurrentSpeed;
        if (speed > 0)
        {
            var intensity = (float) math.log10(speed) / 7.5f - 0.3f;
            intensity = math.clamp(intensity, 0, 1);
            targetWeight = intensity * intensity;
        }
        else targetWeight = 0;
        Volume.weight = math.lerp(Volume.weight, targetWeight,  World.Time.DeltaTime);
    }
}
