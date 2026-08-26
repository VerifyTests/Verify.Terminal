namespace Verify.Terminal.Commands;

public sealed class RejectCommand : ModifyCommand
{
    public override string Verb { get; } = "Reject";
    public override SnapshotAction Action { get; } = SnapshotAction.Reject;

    public RejectCommand(
        SnapshotLocator snapshotLocator,
        SnapshotManager snapshotManager)
            : base(snapshotLocator, snapshotManager)
    {
    }
}