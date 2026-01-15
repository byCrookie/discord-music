using System.Text;

namespace DiscordMusic.Core.YouTube;

internal static class YtdlpArgumentWriter
{
    public static void AppendRuntimeArguments(StringBuilder command, YouTubeOptions options)
    {
        if (options.NoJsRuntimes)
        {
            command.Append(" --no-js-runtimes");
        }
        else
        {
            foreach (var runtime in options.JsRuntimes)
            {
                if (string.IsNullOrWhiteSpace(runtime))
                {
                    continue;
                }

                command.Append(" --js-runtimes ");
                AppendQuoted(command, runtime.Trim());
            }
        }

        if (options.NoRemoteComponents)
        {
            command.Append(" --no-remote-components");
            return;
        }

        foreach (var component in options.RemoteComponents)
        {
            if (string.IsNullOrWhiteSpace(component))
            {
                continue;
            }

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
