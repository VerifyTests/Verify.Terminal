using System.ComponentModel;
using Spectre.IO;

namespace Verify.Terminal.Commands;

public abstract class ModifyCommand : Command<ModifyCommand.Settings>
{
    private readonly SnapshotFinder _snapshotFinder;
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

    protected ModifyCommand(SnapshotFinder snapshotFinder, SnapshotManager snapshotManager)
    {
        _snapshotFinder = snapshotFinder.NotNull();
        _snapshotManager = snapshotManager.NotNull();
    }

    public sealed override int Execute(
        [NotNull] CommandContext context,
        [NotNull] Settings settings)
    {
        // Get all snapshots and show a summary
        var snapshots = _snapshotFinder.Find(settings.Root);
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

        // Process snapshots
        foreach (var snapshot in snapshots)
        {
            if (!_snapshotManager.Process(snapshot, Action))
            {
                AnsiConsole.MarkupLineInterpolated(
                    $"[red]Error:[/] An error occured while processing snapshot: {snapshot.Received}");
                return 2;
            }
        }

        return 0;
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