using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace DiscordMusic.ServiceDefaults;

public static class Extensions
{
    private const string DefaultServiceName = "discord-music";
    private const string DefaultServiceNamespace = "DiscordMusic";

    extension(IHostApplicationBuilder builder)
    {
        public void AddServiceDefaults(string[] sources, string[] meters)
        {
            builder.AddOpenTelemetry(sources, meters);
            builder.Services.AddServiceDiscovery();
            builder.Services.ConfigureHttpClientDefaults(http =>
            {
                http.AddStandardResilienceHandler();
                http.AddServiceDiscovery();
            });
        }

        private void AddOpenTelemetry(string[] sources, string[] meters)
        {
            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
                logging.ParseStateValues = true;
            });

            builder
                .Services.AddOpenTelemetry()
                .ConfigureResource(resource =>
                    resource
                        .AddService(
                            serviceName: DefaultServiceName,
                            serviceNamespace: DefaultServiceNamespace,
                            serviceVersion: IHostApplicationBuilder.GetServiceVersion()
                        )
                        .AddAttributes(
                            IHostApplicationBuilder.GetResourceAttributes(builder.Environment)
                        )
                        .AddTelemetrySdk()
                        .AddEnvironmentVariableDetector()
                )
                .WithMetrics(metrics =>
                {
                    foreach (var meter in meters)
                    {
                        metrics.AddMeter(meter);
                    }

                    metrics.AddRuntimeInstrumentation().AddHttpClientInstrumentation();
                })
                .WithTracing(tracing =>
                {
                    if (builder.Environment.IsDevelopment())
                    {
                        tracing.SetSampler<AlwaysOnSampler>();
                    }

                    foreach (var source in sources)
                    {
                        tracing.AddSource(source);
                    }

                    tracing.AddHttpClientInstrumentation().SetErrorStatusOnException();
                });

            builder.AddOpenTelemetryExporters();
        }

        private void AddOpenTelemetryExporters()
        {
            var useOtlpExporter = !string.IsNullOrWhiteSpace(
                builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
            );

            if (useOtlpExporter)
            {
                builder.Services.AddOpenTelemetry().UseOtlpExporter();
            }
        }

        private static string GetServiceVersion()
        {
            var assembly = Assembly.GetEntryAssembly();
            var informationalVersion = assembly
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                return informationalVersion;
            }

            var assemblyVersion = assembly?.GetName().Version;
            return assemblyVersion is not null ? assemblyVersion.ToString() : "unknown";
        }

        private static IEnumerable<KeyValuePair<string, object>> GetResourceAttributes(
            IHostEnvironment environment
        )
        {
            using var process = Process.GetCurrentProcess();

            return
            [
                new KeyValuePair<string, object>(
                    "deployment.environment.name",
                    environment.EnvironmentName.ToLowerInvariant()
                ),
                new KeyValuePair<string, object>("host.name", Environment.MachineName),
                new KeyValuePair<string, object>(
                    "host.arch",
                    RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()
                ),
                new KeyValuePair<string, object>(
                    "os.type",
                    RuntimeInformation.OSDescription.ToLowerInvariant()
                ),
                new KeyValuePair<string, object>(
                    "os.description",
                    RuntimeInformation.OSDescription
                ),
                new KeyValuePair<string, object>("os.version", Environment.OSVersion.VersionString),
                new KeyValuePair<string, object>(
                    "process.creation.time",
                    process.StartTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
                ),
                new KeyValuePair<string, object>("process.pid", Environment.ProcessId),
                new KeyValuePair<string, object>("process.command", process.ProcessName),
                new KeyValuePair<string, object>("process.runtime.name", ".NET"),
                new KeyValuePair<string, object>(
                    "process.runtime.version",
                    Environment.Version.ToString()
                ),
                new KeyValuePair<string, object>(
                    "process.runtime.description",
                    RuntimeInformation.FrameworkDescription
                ),
            ];
        }
    }
}
