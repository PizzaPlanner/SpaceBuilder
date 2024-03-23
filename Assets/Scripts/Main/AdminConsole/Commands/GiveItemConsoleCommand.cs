using System;
using System.Linq;
using Unity.Entities;

public struct GiveItemConsoleCommand : AdminConsole.IConsoleCommand
{
    public Func<EntityCommandBuffer> GetEcb;
    public string BaseCommand => "give";
    public void Execute(string[] parameters, Entity executor)
    {
        if (parameters.Length < 1) return;
        if(!Identifieable.TryGet(parameters[0], out var id))
            return;
        ushort amount = 1;
        if (parameters.Length > 1)
            ushort.TryParse(parameters[1], out amount);

        ItemTransactionBuilder.Schedule(GetEcb(), TransactionType.Add, executor).With(id, amount);
    }

    public string[] GetHints(int parameter)
    {
        if (parameter == 0)
        {
            var names = Identifieable.AllNames;
            return names.Select(x => x.Value).ToArray();
        }
        return parameter == 1 ? new[]{"10", "100", "1000"} : Array.Empty<string>();
    }
}