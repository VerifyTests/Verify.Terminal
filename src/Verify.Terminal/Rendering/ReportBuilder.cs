using Spectre.Console.Rendering;

namespace Verify.Terminal.Rendering;

public sealed class ReportBuilder
{
    private readonly List<Segment> _buffer;
    private readonly List<SegmentLine> _lines;
    private readonly IAnsiConsole _console;

    public CharacterSet Characters { get; }

    public ReportBuilder(IAnsiConsole console, CharacterSet characters)
    {
        _console = console.NotNull();
        _buffer = new List<Segment>();
        _lines = new List<SegmentLine>();

        Characters = characters.NotNull();
    }

    public void AppendInlineRenderable(IRenderable renderable)
    {
        var segments = renderable.GetSegments(_console).Where(s => !s.IsLineBreak);
        foreach (var segment in segments)
        {
            _buffer.Add(segment.StripLineEndings());
        }
    }

    public void Append(Character character, Color? color = null, Decoration? decoration = null)
    {
        Append(Characters.Get(character), color, decoration);
    }

    public void Append(string text, Color? color = null, Decoration? decoration = null, int? maxLength = null)
    {
        text.NotNull();

        if (maxLength > 0 && (text.Length - 1) > maxLength)
        {
            text = text[..maxLength.Value];
        }

        _buffer.Add(new Segment(text, new Style(foreground: color, decoration: decoration)));
    }

    public void Append(char character, Color? color = null, Decoration? decoration = null)
    {
        _buffer.Add(new Segment(new string(character, 1), new Style(foreground: color, decoration: decoration)));
    }

    public void AppendRepeated(Character character, int count, Color? color = null)
    {
        AppendRepeated(Characters.Get(character), count, color);
    }

    public void AppendRepeated(char character, int count, Color? color = null)
    {
        Append(new string(character, count), color);
    }

    public void AppendSpace()
    {
        Append(" ");
    }

    public void AppendSpaces(int count)
    {
        if (count > 0)
        {
            Append(new string(' ', count));
        }
    }

    public void CommitLine()
    {
        if (_buffer.Count == 0)
        {
            AppendSpace();
        }

        _lines.Add(new SegmentLine(_buffer));
        _buffer.Clear();
    }

    public IReadOnlyList<SegmentLine> GetLines()
    {
        return _lines;
    }

    public IRenderable Build()
    {
        return new ReportRenderable(_lines);
    }
}
