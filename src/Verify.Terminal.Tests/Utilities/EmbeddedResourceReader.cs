using System.IO;
using System.Reflection;
using System;

namespace Verify.Terminal.Tests;

public static class EmbeddedResourceReader
{
    public static string LoadResourceStream(string resourceName)
    {
        if (resourceName is null)
        {
            throw new ArgumentNullException(nameof(resourceName));
        }

        var assembly = Assembly.GetCallingAssembly();
        resourceName = resourceName.Replace("/", ".");

        using (var stream = assembly.GetManifestResourceStream(resourceName))
        using (var reader = new StreamReader(stream))
        {
            return reader.ReadToEnd();
        }
    }
}