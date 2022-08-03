using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class DebugSectorLoadingDisplaySystem : SystemBase
{
    private VisualElement loadingBadge;

    struct UIDelay : IComponentData
    {
        public int Progress;
    }
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate(GetEntityQuery(new EntityQueryDesc{Any = new[]
        {
            ComponentType.ReadOnly<LoadSectorSystem.HasPendingSectors>(),
            ComponentType.ReadOnly<ToGenerate>(),
            ComponentType.ReadOnly<UIDelay>(),
        }}));
    }

    protected override void OnStartRunning()
    {
        loadingBadge ??= GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("SectorLoading");
        loadingBadge.style.display = DisplayStyle.Flex;
        loadingBadge.style.opacity = 0;
        EntityManager.CreateEntity(typeof(UIDelay));
    }

    protected override void OnStopRunning()
    {
        loadingBadge.style.display = DisplayStyle.None;
    }

    protected override void OnUpdate()
    {
        var pendingSectors = HasSingleton<LoadSectorSystem.HasPendingSectors>()
            ? GetSingleton<LoadSectorSystem.HasPendingSectors>().Count
            : 0;
        var toGenerate = GetEntityQuery(ComponentType.ReadOnly<ToGenerate>()).CalculateEntityCount();

        var uiDelay = GetSingleton<UIDelay>();
        if (pendingSectors + toGenerate == 0)
            uiDelay.Progress--;
        else if(uiDelay.Progress < 13)
            uiDelay.Progress++;

        if (uiDelay.Progress <= 0)
        {
            EntityManager.DestroyEntity(GetSingletonEntity<UIDelay>());
            return;
        }

        SetSingleton(uiDelay);
        loadingBadge.style.opacity = math.max(0, (uiDelay.Progress - 3) * 0.1f);
        loadingBadge.Q<Label>().text = $"{toGenerate}:{pendingSectors}";
    }
}
