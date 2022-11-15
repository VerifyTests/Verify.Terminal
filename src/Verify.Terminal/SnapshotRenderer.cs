using System.Linq;
using DiffPlex.DiffBuilder.Model;
using Spectre.Console.Rendering;
using Verify.Terminal.Rendering;

namespace Verify.Terminal;

internal sealed class SnapshotRendererContext
{
    public SnapshotDiff Diff { get; }
    public ReportBuilder Builder { get; }
    public int LineNumberWidth { get; }

    public SnapshotRendererContext(SnapshotDiff diff, ReportBuilder builder)
    {
        Diff = diff ?? throw new ArgumentNullException(nameof(diff));
        Builder = builder ?? throw new ArgumentNullException(nameof(builder));
        LineNumberWidth = (int)(Math.Log10(Diff.New.Count) + 1);
    }
}

public class SnapshotRenderer
{
    private readonly IAnsiConsole _console;

    public SnapshotRenderer(IAnsiConsole console)
    {
        _console = console;
    }

    public IRenderable Render(SnapshotDiff diff, int contextLines)
    {
        var characters = CharacterSet.Create(_console);
        var builder = new ReportBuilder(_console, characters);
        var ctx = new SnapshotRendererContext(diff, builder);

        var marginWidth = (ctx.LineNumberWidth * 2) + 8 + 1;
        var lineNumberWidth = (int)(Math.Log10(diff.New.Count) + 1);

        // Filename
        ctx.Builder.AppendRepeated(Character.HorizontalLine, _console.Profile.Width);
        ctx.Builder.CommitLine();
        ctx.Builder.AppendInlineRenderable(new TextPath(diff.Snapshot.Received.GetFilename().FullPath));
        ctx.Builder.CommitLine();

        // Legend
        ctx.Builder.AppendRepeated(Character.HorizontalLine, _console.Profile.Width);
        ctx.Builder.CommitLine();
        ctx.Builder.Append("-old snapshot", Color.Red);
        ctx.Builder.CommitLine();
        ctx.Builder.Append("+new snapshot", Color.Green);
        ctx.Builder.CommitLine();

        // Top line
        ctx.Builder.AppendRepeated(Character.HorizontalLine, marginWidth);
        ctx.Builder.Append('┬');
        ctx.Builder.Append(Character.HorizontalLine);
        ctx.Builder.Append('┬');
        ctx.Builder.AppendRepeated(Character.HorizontalLine, _console.Profile.Width - (marginWidth + 3));
        ctx.Builder.CommitLine();

        var zip = diff.New.Zip(diff.Old).ToList();

        // Iterate all ranges
        foreach (var (_, _, last, range) in diff.GetRanges(contextLines).Enumerate())
        {
            for (var index = range.Start; index < range.Stop; index++)
            {
                var @new = zip[index].First;
                var old = zip[index].Second;

                if (@new.Type == ChangeType.Modified)
                {
                    // Modified lines
                    RenderLine(ctx, old, index, '-', Color.Red, true, false);
                    RenderLine(ctx, @new, index, '+', Color.Green, false, true);
                }
                else if (@new.Type == ChangeType.Inserted)
                {
                    // Inserted
                    RenderLine(ctx, @new, index, '+', Color.Green, false, true);
                }
                else if (@new.Type == ChangeType.Imaginary)
                {
                    // Modified lines
                    RenderLine(ctx, old, index, '-', Color.Red, true, false);
                }
                else
                {
                    // Unchanged
                    RenderLine(ctx, @new, index, ' ', Color.Grey, true, true);
                }
            }

            // Bottom line
            ctx.Builder.AppendRepeated(Character.HorizontalLine, marginWidth);
            ctx.Builder.Append(last ? '┴' : '┼');
            ctx.Builder.Append(Character.HorizontalLine);
            ctx.Builder.Append(last ? '┴' : '┼');
            ctx.Builder.AppendRepeated(Character.HorizontalLine, _console.Profile.Width - (marginWidth + 3));
            ctx.Builder.CommitLine();
        }

        return ctx.Builder.Build();
    }

    private void RenderLine(
        SnapshotRendererContext ctx, DiffPiece piece,
        int index, char op, Color color,
        bool showLeft, bool showRight)
    {
        var leftLineNumber = showLeft ? (index + 1).ToString() : string.Empty;
        var rightLineNumber = showRight ? (index + 1).ToString() : string.Empty;

        var maxWidth = _console.Profile.Width - (4 + ctx.LineNumberWidth + 4 + ctx.LineNumberWidth + 1 + 1 + 1 + 1);
        var pieces = (piece.Text.Length / maxWidth) + ((piece.Text.Length % maxWidth) == 0 ? 0 : 1);

        for (var i = 0; i < pieces; i++)
        {
            ctx.Builder.AppendSpaces(4);
            ctx.Builder.Append(leftLineNumber.PadLeft(ctx.LineNumberWidth), Color.Navy);
            ctx.Builder.AppendSpaces(4);
            ctx.Builder.Append(rightLineNumber.PadLeft(ctx.LineNumberWidth), Color.Navy);
            ctx.Builder.AppendSpace();
            ctx.Builder.Append(Character.VerticalLine);

            if (i == 0)
            {
                ctx.Builder.Append(op, color);
            }
            else
            {
                ctx.Builder.Append(" ");
            }

            ctx.Builder.Append(Character.VerticalLine);

            var text = new string(piece.Text.Skip(i * maxWidth).Take(maxWidth).ToArray());

            ctx.Builder.Append(text.Replace(" ", "·"), color);
            ctx.Builder.CommitLine();
        }
    }
}