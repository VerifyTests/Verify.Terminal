using System.Collections.Generic;
using System.Linq;
using DiffPlex.DiffBuilder.Model;

namespace Verify.Terminal;

public sealed class SnapshotDiff
{
    public Snapshot Snapshot { get; }
    public List<DiffPiece> Old { get; }
    public List<DiffPiece> New { get; }

    public SnapshotDiff(Snapshot snapshot, List<DiffPiece> old, List<DiffPiece> @new)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        Old = old ?? throw new ArgumentNullException(nameof(old));
        New = @new ?? throw new ArgumentNullException(nameof(@new));
    }

    public List<(int Start, int Stop)> GetRanges(int contextLines)
    {
        var index = 0;
        var start = default(int?);
        var ranges = new List<(int Start, int Stop)>();

        // Only got a new file?
        if (Old.All(x => x.Type == ChangeType.Imaginary))
        {
            return new List<(int Start, int Stop)>
            {
                (0, New.Count - 1),
            };
        }

        while (index < New.Count)
        {
            var type = New[index].Type;
            if (type != ChangeType.Unchanged)
            {
                start = index;
            }
            else
            {
                if (start != null)
                {
                    var rangeStart = Math.Max(0, start.Value - contextLines);
                    var rangeEnd = Math.Min(index + contextLines, New.Count - 1);
                    ranges.Add((rangeStart, rangeEnd));

                    start = null;
                }
            }

            index++;
        }

        return MergeRanges(ranges);
    }

    private static List<(int Start, int Stop)> MergeRanges(List<(int Start, int Stop)> ranges)
    {
        var rangeCount = ranges.Count;
        var totalRanges = 1;

        for (var i = 0; i < rangeCount - 1; i++)
        {
            if (ranges[i].Stop >= ranges[i + 1].Start)
            {
                ranges[i + 1] = (
                    ranges[i].Start,
                    Math.Max(ranges[i].Stop, ranges[i + 1].Stop));

                ranges[i] = (-1, ranges[i].Stop);
            }
            else
            {
                totalRanges++;
            }
        }

        var index = 0;
        var ans = new int[totalRanges][];
        for (var i = 0; i < rangeCount; i++)
        {
            if (ranges[i].Start != -1)
            {
                ans[index] = new int[2] { 0, 0 };

                ans[index][0] = ranges[i].Start;
                ans[index++][1] = ranges[i].Stop;
            }
        }

        var result = new List<(int, int)>();
        for (var i = 0; i < ans.GetLength(0); i++)
        {
            result.Add((ans[i][0], ans[i][1]));
        }

        return result;
    }
}
