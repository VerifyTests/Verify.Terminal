namespace Verify.Terminal.Commands;

public abstract class ModifyCommand : Command<ModifyCommand.Settings>
{
    private readonly SnapshotLocator _snapshotLocator;
    private readonly SnapshotManager _snapshotManager;

    public abstract string Verb { get; }
    public abstract SnapshotAction Action { get; }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-w|--work <DIRECTORY>")]
        [TypeConverter(typeof(DirectoryPathConverter))]
        [Description("The working directory to use")]
        public DirectoryPath? Root { get; set; }

        [CommandOption("-y|--yes")]
        [Description("Confirm all prompts. Chooses affirmative answer instead of prompting.")]
        public bool NoPrompt { get; set; }
    }

    protected ModifyCommand(SnapshotLocator snapshotLocator, SnapshotManager snapshotManager)
    {
        _snapshotLocator = snapshotLocator.NotNull();
        _snapshotManager = snapshotManager.NotNull();
    }

    protected sealed override int Execute(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        // Get all snapshots and show a summary
        var snapshots = _snapshotLocator.Find(settings.Root);
        if (snapshots.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No snapshots found.[/]");
            return 0;
        }

        // Proceed?
        AnsiConsole.Console.ShowSnapshotSummary(snapshots);
        if (!Proceed(settings, $"[yellow]{Verb} {snapshots.Count} snapshot(s)?[/]"))
        {
            return 1;
        }

        // Process snapshots. One that refuses does not stop the rest: an inline snapshot whose
        // frameworks disagreed is skipped rather than picked between, and holding up every other
        // snapshot behind it would mean one unresolvable snapshot blocking the whole command.
        var failed = 0;
        foreach (var snapshot in snapshots)
        {
            var result = _snapshotManager.Process(snapshot, Action);
            if (!result.Succeeded)
            {
                failed++;
                AnsiConsole.Console.ShowSnapshotFailure(snapshot, Action, result);
            }
        }

        if (failed == 0)
        {
            return 0;
        }

        AnsiConsole.MarkupLineInterpolated(
            $"[red]{failed} of {snapshots.Count} snapshot(s) could not be processed.[/]");
        return 2;
    }

    private static bool Proceed(Settings settings, string question)
    {
        if (settings.NoPrompt)
        {
            return true;
        }

        return AnsiConsole.Console.AskYesNo(question);
    }
}
