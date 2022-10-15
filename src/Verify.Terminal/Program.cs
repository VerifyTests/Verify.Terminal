using Spectre.IO;

namespace Verify.Terminal;

public static class Program
{
    public static int Main(string[] args)
    {
        var container = BuildContainer();

        var app = new CommandApp(container);
        app.Configure(config =>
        {
            config.AddCommand<AcceptCommand>("accept");
            config.AddCommand<RejectCommand>("reject");
            config.AddCommand<ReviewCommand>("review");

            config.SetApplicationName("verifyterm");
            config.UseStrictParsing();
        });

        return app.Run(args);
    }

    private static TypeRegistrar BuildContainer()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IEnvironment, Spectre.IO.Environment>();
        services.AddSingleton<IGlobber, Globber>();

        services.AddSingleton<SnapshotFinder>();
        services.AddSingleton<SnapshotManager>();
        services.AddSingleton<SnapshotDiffer>();
        services.AddSingleton<SnapshotRenderer>();

        return new TypeRegistrar(services);
    }
}
