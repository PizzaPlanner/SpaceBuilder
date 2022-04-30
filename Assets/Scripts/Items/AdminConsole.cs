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

[GenerateAuthoringComponent]
public class ExecuteAdminScript : IComponentData
{
    public string Script;
}

public partial class AdminConsoleSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
    }

    private AdminConsole uiRoot;
    protected override void OnStartRunning()
    {
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
                        if (param.Length < 2 || !TryGetId(param[1], out var id)) return;
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

    private bool TryGetId(string s, out long id)
    {
        if (long.TryParse(s, out id))
            return true;

        var query = GetEntityQuery(typeof(Prefab), typeof(Item), typeof(Identifieable), typeof(Name));
        var idents = query.ToComponentDataArray<Identifieable>(Allocator.Temp);
        var names = query.ToComponentDataArray<Name>(Allocator.Temp);
        for(int i = 0; i < names.Length; i++)
            if (names[i].Value.Value == s)
            {
                id = idents[i].ItemId;
                return true;
            }

        return false;
    }

    protected override void OnUpdate()
    {
        if (Input.GetKeyDown(KeyCode.F4))
            uiRoot.style.display = uiRoot.style.display == DisplayStyle.Flex ? DisplayStyle.None : DisplayStyle.Flex;

        Entities.ForEach((Entity e, ExecuteAdminScript script) =>
        {
            uiRoot.OnEnter(script.Script);
            EntityManager.DestroyEntity(e);
        }).WithoutBurst().WithStructuralChanges().Run();
    }
}
