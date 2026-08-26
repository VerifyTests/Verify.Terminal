namespace Verify.Terminal;

public static class ConsoleExtensions
{
    public static void ShowSnapshotSummary(this IAnsiConsole console, IEnumerable<ISnapshot> snapshots)
    {
        var table = new Table();

        table.AddColumn("[blue]Snapshots[/]");
        foreach (var snapshot in snapshots)
        {
            table.AddRow(
                new TextPath(snapshot.Name)
                    .LeafColor(Color.Blue));
        }

        console.Write(table);
    }

    /// <summary>
    /// Reports a snapshot that could not be accepted or rejected, and why when there is a why. The
    /// reason is the interesting half for an inline snapshot, where a refusal usually says what to
    /// do about it.
    /// </summary>
    public static void ShowSnapshotFailure(
        this IAnsiConsole console,
        ISnapshot snapshot,
        SnapshotAction action,
        SnapshotResult result)
    {
        var verb = action == SnapshotAction.Accept ? "accept" : "reject";
        console.MarkupLineInterpolated($"[red]Error:[/] Could not {verb} snapshot: {snapshot.Name}");

        if (result.Message != null)
        {
            console.MarkupLineInterpolated($"[grey]{result.Message}[/]");
        }
    }

    public static bool AskYesNo(this IAnsiConsole console, string question)
    {
        return console.Prompt(new SelectionPrompt<bool>()
           .Title(question)
           .AddChoices(true, false)
           .UseConverter(b => b ? "Yes" : "No"));
    }
}
