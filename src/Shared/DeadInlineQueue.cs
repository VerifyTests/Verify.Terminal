namespace Verify.Terminal.Testing;

/// <summary>
/// Points every inline queue exchange this process makes at a port nothing listens on, so a test is
/// refused instantly instead of reaching whatever tray or viewer happens to be running on the
/// machine - sending it patches, tracked moves and settles for a test's throwaway directory.
/// </summary>
/// <remarks>
/// Compiled into both test projects, because both make those exchanges: the integration suite drives
/// real Verify, and a unit test that applies a staged patch settles it with the owner afterwards.
/// The variable is process wide and DiffEngine reads it on every call, so this is done once through
/// a module initializer, before anything a test does. A scenario that wants an owner stands its own
/// up and points the variable at that instead, then puts this back.
/// </remarks>
public static class DeadInlineQueue
{
    /// <summary>
    /// Where DiffEngine's clients look for the process owning the inline queue. Internal to
    /// DiffEngine but read from the environment on every call, so a test can point every client in
    /// this process somewhere of its own. If DiffEngine renames it these tests break loudly, which
    /// is the kind of assumption this suite exists to pin.
    /// </summary>
    public const string PortVariable = "DiffEngine_ViewerPort";

    [ModuleInitializer]
    internal static void Point() =>
        System.Environment.SetEnvironmentVariable(PortVariable, DeadPort().ToString());

    // A loopback port with nothing behind it: bound to let the OS pick a free one, then released.
    // Something else could bind it afterwards, but this is a test machine and a port just handed out
    // is not one the OS hands out again in a hurry.
    private static int DeadPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint) listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
