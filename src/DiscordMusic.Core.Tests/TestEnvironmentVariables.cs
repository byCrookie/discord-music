using DiscordMusic.Core.Configuration;

namespace DiscordMusic.Core.Tests;

internal sealed class TestEnvironmentVariables(
    IReadOnlyDictionary<string, string?>? variables = null,
    IReadOnlyDictionary<Environment.SpecialFolder, string>? folders = null
) : IEnvironmentVariables
{
    public IReadOnlyDictionary<string, string?> GetVariables()
    {
        return variables ?? new Dictionary<string, string?>();
    }

    public string? GetVariable(string variable)
    {
        return variables?.GetValueOrDefault(variable);
    }

    public string GetFolderPath(Environment.SpecialFolder folder)
    {
        return folders?.GetValueOrDefault(folder) ?? "/";
    }
}
