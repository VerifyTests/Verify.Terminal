namespace Verify.Terminal.Commands;

public sealed class ReviewCommand : Command<ReviewCommand.Settings>
{
    private readonly SnapshotFinder _snapshotFinder;
    private readonly SnapshotDiffer _snapshotDiffer;
    private readonly SnapshotManager _snapshotManager;
    private readonly SnapshotRenderer _snapshotRenderer;

    public ReviewCommand(
        SnapshotFinder snapshotFinder,
        SnapshotDiffer snapshotDiffer,
        SnapshotManager snapshotManager,
        SnapshotRenderer snapshotRenderer)
    {
        _snapshotFinder = snapshotFinder.NotNull();
        _snapshotDiffer = snapshotDiffer.NotNull();
        _snapshotManager = snapshotManager.NotNull();
        _snapshotRenderer = snapshotRenderer.NotNull();
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-w|--work <DIRECTORY>")]
        [TypeConverter(typeof(DirectoryPathConverter))]
        [Description("The working directory to use")]
        public DirectoryPath? Root { get; set; }

        [CommandOption("-c|--context <LINE-COUNT>")]
        [Description("The number of context lines to show. Defaults to 2.")]
        public int ContextLines { get; set; } = 2;
    }

    public override int Execute(
        [NotNull] CommandContext context,
        [NotNull] Settings settings)
    {
        // Get all snapshots and show a summary
        var snapshots = _snapshotFinder.Find(settings.Root);
        if (snapshots.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No snapshots to review.[/]");
            return 0;
        }

        foreach (var (index, first, last, snapshot) in snapshots.Enumerate())
        {
            var diff = _snapshotDiffer.Diff(snapshot);

            if (!first)
            {
                AnsiConsole.WriteLine();
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[yellow b]Reviewing[/] [[{index + 1}/{snapshots.Count}]]");

RenderDiff:
            AnsiConsole.Write(_snapshotRenderer.Render(diff, Math.Max(0, settings.ContextLines)));
            ShowHelp();

            switch (ShowPrompt())
            {
                case SnapshotAction.Accept:
                    _snapshotManager.Accept(snapshot);
                    break;
                case SnapshotAction.Reject:
                    _snapshotManager.Reject(snapshot);
                    break;
                case SnapshotAction.Skip:
                    continue;
                case SnapshotAction.Unknown:
                    goto RenderDiff;
            }

            if (!last)
            {
                AnsiConsole.WriteLine();
            }
        }

        return 0;
    }

    private static SnapshotAction ShowPrompt()
    {
        AnsiConsole.Markup("Accept this change [[[green]a[/],[red]r[/],[yellow]s[/]]]? ");

        var answer = Console.ReadLine()?.ToLowerInvariant();

        return answer switch
        {
            "a" => SnapshotAction.Accept,
            "accept" => SnapshotAction.Accept,

            "r" => SnapshotAction.Reject,
            "reject" => SnapshotAction.Reject,

            "s" => SnapshotAction.Skip,
            "skip" => SnapshotAction.Skip,

            _ => SnapshotAction.Unknown,
        };
    }

    private static void ShowHelp()
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().PadLeft(4));
        grid.AddColumn(new GridColumn().PadLeft(4));
        grid.AddRow("[[[green]a[/]]]ccept", "[grey]keep the new snapshot[/]");
        grid.AddRow("[[[red]r[/]]]eject", "[grey]keep the old snapshot[/]");
        grid.AddRow("[[[yellow]s[/]]]kip", "[grey]keep both for now[/]");

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine();
    }
}