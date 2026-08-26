namespace Verify.Terminal.Commands;

public sealed class ReviewCommand : Command<ReviewCommand.Settings>
{
    private readonly SnapshotLocator _snapshotLocator;
    private readonly SnapshotDiffer _snapshotDiffer;
    private readonly SnapshotManager _snapshotManager;
    private readonly SnapshotRenderer _snapshotRenderer;

    public ReviewCommand(
        SnapshotLocator snapshotLocator,
        SnapshotDiffer snapshotDiffer,
        SnapshotManager snapshotManager,
        SnapshotRenderer snapshotRenderer)
    {
        _snapshotLocator = snapshotLocator.NotNull();
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

    protected override int Execute(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        // Get all snapshots and show a summary
        var snapshots = _snapshotLocator.Find(settings.Root);
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
            AnsiConsole.Write(_snapshotRenderer.Render(diff, Math.Max(0, settings.ContextLines)));

            var action = ShowPrompt();
            if (action == SnapshotAction.Skip)
            {
                continue;
            }

            // Reported rather than returned on, since the rest of the review is still worth doing.
            // An inline snapshot refuses for reasons that are about it alone: the source moved since
            // the test ran, or its frameworks disagreed about the content.
            var result = _snapshotManager.Process(snapshot, action);
            if (!result.Succeeded)
            {
                AnsiConsole.Console.ShowSnapshotFailure(snapshot, action, result);
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
        var grid = new Grid();
        grid.AddColumn(new GridColumn().PadLeft(4));
        grid.AddColumn(new GridColumn().PadLeft(4));
        grid.AddRow("[[[green]a[/]]]ccept", "[grey]keep the new snapshot[/]");
        grid.AddRow("[[[red]r[/]]]eject", "[grey]keep the old snapshot[/]");
        grid.AddRow("[[[yellow]s[/]]]kip", "[grey]keep both for now[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        AnsiConsole.Write(grid);

        try
        {
            AnsiConsole.Cursor.Hide();

            while (true)
            {
                var key = Console.ReadKey(true);
                var action = key.Key switch
                {
                    ConsoleKey.A => SnapshotAction.Accept,
                    ConsoleKey.R => SnapshotAction.Reject,
                    ConsoleKey.S => SnapshotAction.Skip,
                    _ => (SnapshotAction?)null,
                };

                if (action != null)
                {
                    return action.Value;
                }
            }
        }
        finally
        {
            AnsiConsole.Cursor.Show();
        }
    }
}
