using System.Collections.Generic;
using Spectre.Console.Rendering;

namespace Verify.Terminal.Rendering;

internal sealed class ReportRenderable : IRenderable
{
    private readonly List<SegmentLine> _lines;

    public ReportRenderable(IEnumerable<SegmentLine> lines)
    {
        if (lines is null)
        {
            throw new ArgumentNullException(nameof(lines));
        }

        _lines = new List<SegmentLine>(lines);
    }

    public Measurement Measure(RenderOptions context, int maxWidth)
    {
        return new Measurement(maxWidth, maxWidth);
    }

    public IEnumerable<Segment> Render(RenderOptions context, int maxWidth)
    {
        return new SegmentLineEnumerator(_lines);
    }
}