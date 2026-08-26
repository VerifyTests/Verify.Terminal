namespace Verify.Terminal.Tests;

// Stands in for the process holding the inline queue. Every call on the real one is a loopback
// exchange with another process, which a unit test has none of.
internal sealed class FakeInlineQueueOwner : IInlineQueueOwner
{
    private readonly List<PendingInline> _pending = [];

    // False until something is queued, which is what a machine with no owner answers.
    public bool HasOwner { get; set; }

    public InlineAcceptOutcome AcceptOutcome { get; set; } = InlineAcceptOutcome.Accepted;

    public string? AcceptMessage { get; set; }

    public bool DiscardResult { get; set; } = true;

    public string? DiscardMessage { get; set; }

    public bool? StillPendingResult { get; set; }

    public List<string> Accepted { get; } = [];

    public List<string> Discarded { get; } = [];

    public void Queue(InlinePatch patch)
    {
        HasOwner = true;
        _pending.Add(new(patch));
    }

    public void Queue(params InlinePatch[] variants)
    {
        HasOwner = true;
        _pending.Add(
            new(variants.Select(_ => new InlineVariant(_, _.Framework == null ? [] : [_.Framework])).ToList()));
    }

    public bool TryList(out IReadOnlyList<PendingInline> pending)
    {
        pending = _pending;
        return HasOwner;
    }

    public InlineAcceptOutcome Accept(string key, out string? message)
    {
        Accepted.Add(key);
        message = AcceptMessage;
        return AcceptOutcome;
    }

    public bool Discard(string key, out string? message)
    {
        Discarded.Add(key);
        message = DiscardMessage;
        return DiscardResult;
    }

    public bool? StillPending(string key) => StillPendingResult;
}
