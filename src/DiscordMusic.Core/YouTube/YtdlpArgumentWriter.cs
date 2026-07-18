using System.Text;

namespace DiscordMusic.Core.YouTube;

internal static class YtdlpArgumentWriter
{
    public static IEnumerable<string> RuntimeArguments(YouTubeOptions options)
    {
        if (options.NoJsRuntimes)
        {
            yield return "--no-js-runtimes";
        }
        else
        {
            foreach (
                var runtime in options.JsRuntimes.Where(runtime =>
                    !string.IsNullOrWhiteSpace(runtime)
                )
            )
            {
                yield return "--js-runtimes";
                yield return runtime.Trim();
            }
        }

        if (options.NoRemoteComponents)
        {
            yield return "--no-remote-components";
            yield break;
        }

        foreach (
            var component in options.RemoteComponents.Where(component =>
                !string.IsNullOrWhiteSpace(component)
            )
        )
        {
            yield return "--remote-components";
            yield return component.Trim();
        }
    }

    public static void AppendRuntimeArguments(StringBuilder command, YouTubeOptions options)
    {
        if (options.NoJsRuntimes)
        {
            command.Append(" --no-js-runtimes");
        }
        else
        {
            foreach (
                var runtime in options.JsRuntimes.Where(runtime =>
                    !string.IsNullOrWhiteSpace(runtime)
                )
            )
            {
                command.Append(" --js-runtimes ");
                AppendQuoted(command, runtime.Trim());
            }
        }

        if (options.NoRemoteComponents)
        {
            command.Append(" --no-remote-components");
            return;
        }

        foreach (
            var component in options.RemoteComponents.Where(component =>
                !string.IsNullOrWhiteSpace(component)
            )
        )
        {
            command.Append(" --remote-components ");
            AppendQuoted(command, component.Trim());
        }
    }

    private static void AppendQuoted(StringBuilder command, string value)
    {
        command.Append('"');
        command.Append(value.Replace("\\", "\\\\").Replace("\"", "\\\""));
        command.Append('"');
    }
}
