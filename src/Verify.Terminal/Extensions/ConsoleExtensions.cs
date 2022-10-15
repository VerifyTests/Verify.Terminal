using System.Collections.Generic;

namespace Verify.Terminal;

public static class ConsoleExtensions
{
    public static void ShowSnapshotSummary(this IAnsiConsole console, IEnumerable<Snapshot> snapshots)
    {
        var table = new Table();

        table.AddColumn("[blue]Snapshots[/]");
        foreach (var snapshot in snapshots)
        {
            table.AddRow(
                new TextPath(snapshot.Received.FullPath)
                    .LeafColor(Color.Blue));
        }

        console.Write(table);
    }

    public static bool AskYesNo(this IAnsiConsole console, string question)
    {
        return console.Prompt(new SelectionPrompt<bool>()
           .Title(question)
           .AddChoices(true, false)
           .UseConverter(b => b ? "Yes" : "No"));
    }
}
