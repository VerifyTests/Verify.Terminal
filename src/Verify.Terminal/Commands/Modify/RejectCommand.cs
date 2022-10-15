namespace Verify.Terminal.Commands;

public sealed class RejectCommand : ModifyCommand
{
    public override string Verb { get; } = "Reject";
    public override SnapshotAction Action { get; } = SnapshotAction.Reject;

    public RejectCommand(
        SnapshotFinder snapshotFinder,
        SnapshotManager snapshotManager)
            : base(snapshotFinder, snapshotManager)
    {
    }
}