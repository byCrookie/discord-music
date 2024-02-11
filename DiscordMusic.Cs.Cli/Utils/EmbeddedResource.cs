using System.Reflection;

namespace DiscordMusic.Cs.Cli.Utils;

public static class EmbeddedResource
{
    public static async Task<string> ReadAsync(Assembly assembly, string path)
    {
        var names = assembly.GetManifestResourceNames();
        var resource = names.FirstOrDefault(name => name == $"{assembly.GetName().Name}.{path}");

        if (resource == null)
        {
            throw new InvalidOperationException($"Resource '{path}' not found in assembly '{assembly.FullName}'");
        }

        await using var stream = assembly.GetManifestResourceStream(resource);
        if (stream == null)
        {
            throw new InvalidOperationException($"Resource '{path}' not found in assembly '{assembly.FullName}'");
        }

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}