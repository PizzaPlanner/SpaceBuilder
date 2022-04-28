using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class AdminConsole : VisualElement
{
    public Action<string> OnEnter;
    public AdminConsole()
    {
        AddToClassList("admin-console");
        ScrollView scroller = new ScrollView(ScrollViewMode.Vertical);
        scroller.AddToClassList("log");
        TextField commandBox = new TextField();
        commandBox.AddToClassList("input");
        Add(scroller);
        Add(commandBox);
        commandBox.RegisterCallback<KeyDownEvent>((x) =>
        {
            if (x.keyCode != KeyCode.KeypadEnter && x.keyCode != KeyCode.Return)
                return;

            var text = commandBox.value;
            scroller.contentContainer.Add(new Label(text));
            OnEnter?.Invoke(text);
            commandBox.value = "";
        });
    }

    public new class UxmlFactory : UxmlFactory<AdminConsole, UxmlTraits> { }
}

[AlwaysUpdateSystem]
public partial class AdminConsoleSystem : SystemBase
{
    private AdminConsole uiRoot;
    private NativeArray<Identifieable> itemPrefabs;
    protected override void OnStartRunning()
    {
        itemPrefabs = GetEntityQuery(typeof(Prefab), typeof(Item), typeof(Identifieable))
            .ToComponentDataArray<Identifieable>(Allocator.Persistent);
        if (uiRoot == null)
        {
            uiRoot = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q<AdminConsole>();
            uiRoot.OnEnter = (input) =>
            {
                var param = input.Split(' ');
                if (param.Length == 0)
                    return;
                switch (param[0])
                {
                    case "give":
                        if (param.Length < 2 || !long.TryParse(param[1], out var id)) return;
                        ushort amount = 1;
                        if (param.Length > 2)
                            ushort.TryParse(param[2], out amount);
                        var e = EntityManager.CreateEntity(typeof(GetItemAction));
                        EntityManager.SetComponentData(e, new GetItemAction
                        {
                            Inventory = GetSingletonEntity<LocalPlayer>(),
                            Quantity = amount,
                            ItemId = id
                        });
                        break;
                }
            };
        }
        uiRoot.style.display = DisplayStyle.None;
    }

    protected override void OnStopRunning()
    {
        itemPrefabs.Dispose();
    }

    protected override void OnUpdate()
    {
        if (Input.GetKeyDown(KeyCode.F4))
            uiRoot.style.display = uiRoot.style.display == DisplayStyle.Flex ? DisplayStyle.None : DisplayStyle.Flex;
    }
}
