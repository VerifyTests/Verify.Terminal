namespace Verify.Terminal;

public sealed class SnapshotFinder
{
    // The runtime names that Verify appends to a snapshot name.
    // `Core` is only emitted by older versions of Verify.
    private static readonly string[] _runtimes = ["DotNet", "Net", "Mono", "Core"];

    // Windows paths are case insensitive.
    private static readonly StringComparison _pathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly IGlobber _globber;
    private readonly IEnvironment _environment;

    public SnapshotFinder(
        IGlobber globber,
        IEnvironment environment)
    {
        _globber = globber.NotNull();
        _environment = environment.NotNull();
    }

    public ISet<Snapshot> Find(DirectoryPath? root = null)
    {
        root ??= _environment.WorkingDirectory;
        root = root.MakeAbsolute(_environment);

        // Verify records the verified file each received file belongs to, so prefer that over any
        // guess. See https://github.com/VerifyTests/Verify/issues/1809
        var maps = ReceivedMaps.Read(root.FullPath);

        // Older versions of Verify wrote no maps, and a map is not written on a build server, so fall
        // back to matching each received file against the verified files that exist alongside it. The
        // verified name cannot be reliably reconstructed from the received name, so this is a guess.
        var verifiedByDirectory = Match(root, "**/*.verified.*", "verified")
            .GroupBy(_ => _.Directory, StringComparer.Ordinal)
            .ToDictionary(_ => _.Key, _ => _.ToList(), StringComparer.Ordinal);

        var result = new HashSet<Snapshot>();
        foreach (var received in Match(root, "**/*.received.*", "received"))
        {
            var (verifiedPath, isRerouted) = GetVerified(received, maps, verifiedByDirectory);
            result.Add(new Snapshot(received.Path, verifiedPath, isRerouted));
        }

        return result;
    }

    private IEnumerable<ParsedName> Match(DirectoryPath root, string pattern, string marker) =>
        _globber
            .Match(pattern, new GlobberSettings { Root = root })
            .OfType<FilePath>()
            .Select(_ => ParsedName.Parse(_, marker));

    private (FilePath VerifiedPath, bool IsRerouted) GetVerified(
        ParsedName received,
        ReceivedMaps maps,
        Dictionary<string, List<ParsedName>> verifiedByDirectory)
    {
        // Verify recorded the pair, so there is nothing to work out.
        if (maps.TryGetVerified(received.Path.FullPath, out var mapped))
        {
            var verified = new FilePath(mapped);
            var isRerouted = !verified.FullPath.Equals(LiteralVerified(received).FullPath, _pathComparison);
            return (verified, isRerouted);
        }

        var candidates = verifiedByDirectory.TryGetValue(received.Directory, out var inDirectory)
            ? inDirectory.Where(_ => _.Extension == received.Extension).ToList()
            : [];

        // An exact match is never rerouted.
        var exact = candidates.FirstOrDefault(_ => _.Stem == received.Stem);
        if (exact != null)
        {
            return (exact.Path, false);
        }

        // Otherwise find the most specific verified file that the received file reduces to, ie. the
        // verified file whose name is the received name with a less specific runtime and/or with some
        // parameters dropped (`IgnoreParameters`, `IgnoreParametersForVerified`).
        var best = candidates
            .Select(_ => (Verified: _, Score: ReductionScore(received, _)))
            .Where(_ => _.Score >= 0)
            .OrderByDescending(_ => _.Score)
            .ThenBy(_ => _.Verified.Stem, StringComparer.Ordinal)
            .FirstOrDefault();
        if (best.Verified != null)
        {
            return (best.Verified.Path, true);
        }

        // Older versions of Verify left the runtime out of the received file when the test project
        // targets a single framework, eg. `Foo.received.txt` -> `Foo.DotNet.verified.txt`
        if (received.RuntimeFull.Length == 0)
        {
            foreach (var runtime in _runtimes)
            {
                var appended = candidates.FirstOrDefault(_ => _.Stem == $"{received.Stem}.{runtime}");
                if (appended != null)
                {
                    return (appended.Path, true);
                }
            }
        }

        // No verified file exists yet: fall back to the name the received file maps to directly.
        return (LiteralVerified(received), false);
    }

    // How specifically `received` reduces to `verified`. Higher is more specific; -1 if incompatible.
    private static int ReductionScore(ParsedName received, ParsedName verified)
    {
        // The `#name`/`#index` suffix for multi-target files is identical on both sides.
        if (received.Index != verified.Index)
        {
            return -1;
        }

        // The verified name keeps a subset of the received parameters, so its head has to be the
        // received head with zero or more trailing parameters removed.
        if (!IsBoundedPrefix(received.Head, verified.Head))
        {
            return -1;
        }

        var runtimeScore = RuntimeScore(received, verified);
        if (runtimeScore < 0)
        {
            return -1;
        }

        // Prefer retaining more of the received name (more parameters), then a more specific runtime.
        return (verified.Head.Length * 10) + runtimeScore;
    }

    // Whether `prefix` is `full`, or `full` truncated at a parameter (`_`) boundary. The boundary
    // check stops `Foo` from matching `FooBar`.
    private static bool IsBoundedPrefix(string full, string prefix)
    {
        if (prefix.Length > full.Length ||
            !full.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        return prefix.Length == full.Length ||
               full[prefix.Length] == '_';
    }

    // Whether the verified runtime is a reduction of the received runtime, and how close.
    private static int RuntimeScore(ParsedName received, ParsedName verified)
    {
        // Same runtime, eg. received and verified both `.DotNet11_0`, or both have none.
        if (received.RuntimeFull == verified.RuntimeFull)
        {
            return 2;
        }

        // Verified dropped the runtime, eg. received `.DotNet11_0` -> verified none.
        if (verified.RuntimeFull.Length == 0)
        {
            return 0;
        }

        // `UniqueForRuntime`, eg. received `.DotNet11_0` -> verified `.DotNet`.
        if (verified.RuntimeFull == received.RuntimeBare)
        {
            return 1;
        }

        return -1;
    }

    private static FilePath LiteralVerified(ParsedName received) =>
        new($"{received.Directory}/{received.Stem}.verified.{received.Extension}");

    // Verify formats the version as eg. `10_0` in `.DotNet10_0`, or `4_8` in `.Net4_8`
    private static bool IsVersion(string version)
    {
        var parts = version.Split('_');
        return parts.Length == 2 &&
               parts.All(part => part.Length > 0 && part.All(char.IsAsciiDigit));
    }

    // A received or verified file name split into the parts that can differ between the two.
    private sealed class ParsedName
    {
        public FilePath Path { get; }
        public string Directory { get; }
        public string Stem { get; }
        public string Extension { get; }

        // The trailing `#name`/`#index` for multi-target files, or empty.
        public string Index { get; }

        // The name without the runtime token and index, ie. `{TypeAndMethod}{Parameters}`.
        public string Head { get; }

        // The runtime token including any version, eg. `.DotNet11_0`, `.DotNet`, or empty.
        public string RuntimeFull { get; }

        // The runtime token without the version, eg. `.DotNet`, or empty.
        public string RuntimeBare { get; }

        private ParsedName(
            FilePath path, string stem, string extension,
            string index, string head, string runtimeFull, string runtimeBare)
        {
            Path = path;
            Directory = path.GetDirectory().FullPath;
            Stem = stem;
            Extension = extension;
            Index = index;
            Head = head;
            RuntimeFull = runtimeFull;
            RuntimeBare = runtimeBare;
        }

        public static ParsedName Parse(FilePath path, string marker)
        {
            var filename = path.GetFilename().FullPath;

            var token = $".{marker}.";
            var markerIndex = filename.IndexOf(token, StringComparison.Ordinal);
            var stem = filename[..markerIndex];
            var extension = filename[(markerIndex + token.Length)..];

            var (index, head, runtimeFull, runtimeBare) = Decompose(stem);
            return new(path, stem, extension, index, head, runtimeFull, runtimeBare);
        }

        private static (string Index, string Head, string RuntimeFull, string RuntimeBare) Decompose(string stem)
        {
            var index = string.Empty;
            var core = stem;

            var hash = stem.IndexOf('#');
            if (hash >= 0)
            {
                index = stem[hash..];
                core = stem[..hash];
            }

            var lastDot = core.LastIndexOf('.');
            if (lastDot >= 0)
            {
                var segment = core[(lastDot + 1)..];
                foreach (var runtime in _runtimes)
                {
                    if (!segment.StartsWith(runtime, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var version = segment[runtime.Length..];
                    if (version.Length == 0 || IsVersion(version))
                    {
                        return (index, core[..lastDot], $".{segment}", $".{runtime}");
                    }
                }
            }

            return (index, core, string.Empty, string.Empty);
        }
    }
}
