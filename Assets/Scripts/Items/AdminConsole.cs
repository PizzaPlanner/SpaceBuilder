using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

public class AdminConsole : VisualElement
{
    public Entity LocalPlayer;

    public interface IConsoleCommand
    {
        public string BaseCommand { get; }
        void Execute(string[] parameters, Entity executor);
    }

    public AdminConsole()
    {
        commands = new Dictionary<string, IConsoleCommand>();

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
            {
                Parse(commandBox);
                return;
            }

            var text = commandBox.value;
            scroller.contentContainer.Add(new Label(text));
            commandBox.value = "";
            Execute(text, LocalPlayer);
        });
    }

    private Dictionary<string, IConsoleCommand> commands;

    public void Execute(string text, Entity executor)
    {
        var tokens = Parse(text);

        if (tokens.Count == 0 || tokens[0].Type != TokenType.Command)
            return;

        var cmd = tokens[0].Content;
        tokens.RemoveAt(0);
        // Debug.Log($"Executing {cmd}({string.Join(", ", tokens.Select(x => $"'{x.Content}'"))})");
        commands[cmd].Execute(tokens.Select(x => x.Content).ToArray(), executor);
    }

    public enum TokenType
    {
        Command,
        Text,
        Number
    }

    public struct Token
    {
        public TokenType Type;
        public string Content;
    }

    public void Register(IConsoleCommand command)
    {
        commands[command.BaseCommand] = command;
    }

    void Parse(TextField text)
    {
        var tokens = Parse(text.text);
    }

    List<Token> Parse(string text)
    {
        var tokens = new List<Token>();
        var parts = text.Split(' ');
        if (parts.Length == 0)
            return tokens;

        bool continueWithLastToken = false;
        string lastToken = "";
        for (int i = 0; i < parts.Length; i++)
        {
            var command = parts[i].Trim();
            if (string.IsNullOrWhiteSpace(command))
                continue;

            var type = continueWithLastToken
                ? TokenType.Text
                : i == 0 && commands.ContainsKey(command)
                    ? TokenType.Command
                    : decimal.TryParse(command, out _)
                        ? TokenType.Number
                        : TokenType.Text;

            if (command.StartsWith("\"") && !continueWithLastToken)
            {
                continueWithLastToken = true;
                lastToken = command;
            }
            else if (continueWithLastToken)
                lastToken += " " + command;

            if (continueWithLastToken && command.EndsWith("\""))
            {
                command = lastToken.Trim('"');
                continueWithLastToken = false;
                lastToken = "";
            }

            if(!continueWithLastToken)
                tokens.Add(new Token{Content = command, Type = type});
        }

        return tokens;
    }

    public new class UxmlFactory : UxmlFactory<AdminConsole, UxmlTraits> { }
}

[GenerateAuthoringComponent]
public class ExecuteAdminScript : IComponentData
{
    [Multiline]
    public string Script;
    public Entity Executor;
}

public struct MoveSectorCommand : AdminConsole.IConsoleCommand
{
    public EntityManager EntityManager;
    public string BaseCommand => "warp";
    public void Execute(string[] parameters, Entity executor)
    {
        if (parameters.Length != 3 && parameters.Length != 6)
            return;

        int ssx = 0;
        int ssy = 0;
        int ssz = 0;

        if (!int.TryParse(parameters[0], out var x) ||
            !int.TryParse(parameters[1], out var y) ||
            !int.TryParse(parameters[2], out var z))
            return;
        if(parameters.Length == 6)
            if (!int.TryParse(parameters[3], out ssx) ||
                !int.TryParse(parameters[4], out ssy) ||
                !int.TryParse(parameters[5], out ssz))
                return;

        var ltg2 = new LocalToGalaxy{Sector = new int3(x, y, z), SubSector = new int3(ssx, ssy, ssz)};
        EntityManager.SetComponentData(executor, ltg2);
        var e = EntityManager.CreateEntity(typeof(SectorChange));
        EntityManager.SetComponentData(e, new SectorChange
        {
            Entity = executor,
            New = ltg2,
            Previous = EntityManager.GetComponentData<LocalToGalaxy>(executor)
        });
    }
}

public struct GiveItemConsoleCommand : AdminConsole.IConsoleCommand
{
    public Func<string, ulong> GetId;
    public EntityManager EntityManager;
    public string BaseCommand => "give";
    public void Execute(string[] parameters, Entity executor)
    {
        if (parameters.Length < 1) return;
        var id = GetId(parameters[0]);
        if (id == 0) return;
        ushort amount = 1;
        if (parameters.Length > 1)
            ushort.TryParse(parameters[1], out amount);
        var e = EntityManager.CreateEntity(typeof(GetItemAction));
        EntityManager.SetComponentData(e, new GetItemAction
        {
            Inventory = executor,
            Quantity = amount,
            ItemId = id
        });
    }
}

public struct NoclipConsoleCommand : AdminConsole.IConsoleCommand
{
    public EntityManager EntityManager;
    public string BaseCommand => "noclip";
    public void Execute(string[] parameters, Entity executor)
    {
        bool on = !(parameters.Length == 1 && ushort.TryParse(parameters[0], out var i) && i == 0);
        EntityManager.SetSharedComponentData(executor, new PhysicsWorldIndex{Value = on ? 0u : 1u});
    }
}

public struct SpawnCommand : AdminConsole.IConsoleCommand
{
    public EntityManager EntityManager;
    public string BaseCommand => "spawn";
    public void Execute(string[] parameters, Entity executor)
    {
        if (parameters.Length == 0 || executor == Entity.Null || !EntityManager.HasComponent<LocalToGalaxy>(executor))
            return;

        var path = parameters[0];
        var e = BlueprintLibrary.Load(path, EntityManager);
        if (e == Entity.Null)
            return;
        var ltg = EntityManager.GetComponentData<LocalToGalaxy>(executor);
        EntityManager.SetComponentData(e, EntityManager.GetComponentData<Rotation>(executor));
        EntityManager.SetComponentData(e, ltg);
    }
}

public partial class AdminConsoleSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
        RequireSingletonForUpdate<IdentifiableMappingSystem.Loaded>();
    }

    private AdminConsole uiRoot;
    protected override void OnStartRunning()
    {
        if (uiRoot == null)
        {
            uiRoot = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q<AdminConsole>();
            uiRoot.LocalPlayer = GetSingletonEntity<LocalPlayer>();
            uiRoot.Register(new GiveItemConsoleCommand{EntityManager = EntityManager, GetId = GetId});
            uiRoot.Register(new NoclipConsoleCommand{EntityManager = EntityManager});
            uiRoot.Register(new SpawnCommand(){EntityManager = EntityManager});
            uiRoot.Register(new MoveSectorCommand{EntityManager = EntityManager});
        }
        uiRoot.style.display = DisplayStyle.None;
    }

    public ulong GetId(string s)
    {
        if (ulong.TryParse(s, out var id))
            return id;

        var query = GetEntityQuery(typeof(Prefab), typeof(Item), typeof(Identifieable), typeof(Name));
        var idents = query.ToComponentDataArray<Identifieable>(Allocator.Temp);
        var names = query.ToComponentDataArray<Name>(Allocator.Temp);
        for(int i = 0; i < names.Length; i++)
            if (names[i].Value.Value == s)
            {
                return idents[i].ItemId;
            }

        return 0;
    }

    protected override void OnUpdate()
    {
        if (Input.GetKeyDown(KeyCode.F4))
        {
            uiRoot.style.display = uiRoot.style.display == DisplayStyle.Flex ? DisplayStyle.None : DisplayStyle.Flex;
            if (uiRoot.style.display == DisplayStyle.Flex)
            {
                World.GetExistingSystem<InputSystem>().SetCursor(InputSystem.CursorState.Default);
                uiRoot.Q<TextField>().Focus();
            }
            else
            {
                World.GetExistingSystem<InputSystem>().SetCursor(InputSystem.CursorState.Disabled);
            }

        }

        Entities.ForEach((Entity e, ExecuteAdminScript script) =>
        {
            uiRoot.Execute(script.Script, script.Executor == Entity.Null ? GetSingletonEntity<LocalPlayer>() : script.Executor);
            EntityManager.DestroyEntity(e);
        }).WithoutBurst().WithStructuralChanges().Run();
    }
}
