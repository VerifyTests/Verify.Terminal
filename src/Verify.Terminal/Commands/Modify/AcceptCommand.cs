namespace Verify.Terminal.Commands;

public sealed class AcceptCommand : ModifyCommand
{
    public override string Verb { get; } = "Accept";
    public override SnapshotAction Action { get; } = SnapshotAction.Accept;

    public AcceptCommand(
        SnapshotFinder snapshotFinder,
        SnapshotManager snapshotManager)
            : base(snapshotFinder, snapshotManager)
    {
    }
}