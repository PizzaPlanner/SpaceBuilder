using Galaxy.SectorLoading;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class AdminConsole : VisualElement
{
    public Entity LocalPlayer;

    public interface IConsoleCommand
    {
        public string BaseCommand { get; }
        void Execute(string[] parameters, Entity executor);
        string[] GetHints(int parameter);
    }

    public AdminConsole()
    {
        AddToClassList("admin-console");
        var scroller = new ScrollView(ScrollViewMode.Vertical);
        scroller.AddToClassList("log");
        var commandBox = new TextField();
        commandBox.selectAllOnFocus = true;
        commandBox.AddToClassList("input");
        Add(scroller);
        Add(commandBox);
        commandBox.RegisterCallback<KeyDownEvent>((evt) => { 

            if (evt.keyCode is KeyCode.KeypadEnter or KeyCode.Return  || evt.character == '\n')
            {
                // Submit logic
                evt.StopPropagation();
                
                var text = commandBox.value;
                scroller.contentContainer.Add(new Label(text));
                commandBox.value = "";
                Execute(text, LocalPlayer);
                return;
            }

            if (evt.keyCode == KeyCode.Tab || evt.character == '\t')
            {
                // Focus logic
                evt.StopPropagation();
                InputNextHint();
                return;
            }
            
            Parse(commandBox);
        }, TrickleDown.TrickleDown);
    }

    private static Dictionary<string, IConsoleCommand> commands = new Dictionary<string, IConsoleCommand>();
    
    void InputNextHint()
    {
        var tf = this.Q<TextField>();
        var lastword = tf.value.Split(' ').LastOrDefault() ?? "";
        var withoutLastword = tf.value[..^lastword.Length];

        var possibleWords = GetPossibleWords(withoutLastword).Select(x => x.Contains(" ") ? $"\"{x}\"" : x).ToArray();
        var nextword = "";
        for (int i = 0; i < possibleWords.Length; i++)
        {
            var prevI = (i - 1 + possibleWords.Length) % possibleWords.Length;
            if (lastword == possibleWords[prevI])
            {
                nextword = possibleWords[i];
                break;
            } else if (possibleWords[i].StartsWith(lastword))
            {
                nextword = possibleWords[i];
            }
        }

        if (nextword == "" && possibleWords.Length > 0)
            nextword = possibleWords[0];

        tf.value = tf.value.Substring(0, tf.value.LastIndexOf(' ') + 1) + nextword;
        tf.SelectRange(tf.value.Length, tf.value.Length);
    }

    public static void Execute(string text, Entity executor)
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

    public string[] GetPossibleWords(string text)
    {
        var tokens = Parse(text);
        var command = tokens.FirstOrDefault(x => x.Type == TokenType.Command);
        Debug.Log($"Showing Hints for '{command.Content}'");
        if (string.IsNullOrWhiteSpace(command.Content))
            return commands.Keys.ToArray();
        return commands[command.Content].GetHints(tokens.Count - 1);
    }

    public struct Token
    {
        public TokenType Type;
        public string Content;
    }

    public static void Register(IConsoleCommand command)
    {
        commands[command.BaseCommand] = command;
    }

    void Parse(TextField text)
    {
        var tokens = Parse(text.text);
    }

    static List<Token> Parse(string text)
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