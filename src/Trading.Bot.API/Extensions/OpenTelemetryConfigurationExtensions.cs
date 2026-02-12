namespace Trading.Bot.API.Extensions;

public static class OpenTelemetryConfigurationExtensions
{
    public static void AddOpenTelemetry(this WebApplicationBuilder builder)
    {
        const string serviceName = "Trading.Bot.API";
        
        var otlpEndpoint = new Uri("http://localhost:4317");

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource.AddService(serviceName);
            })
            .WithTracing(tracing =>
                tracing
                    .AddHttpClientInstrumentation()
                    .AddSource(ApplicationDiagnostics.ActivitySourceName)
                    .SetSampler(new AlwaysOnSampler())
                    .AddOtlpExporter(options =>
                        options.Endpoint = otlpEndpoint)
            )
            .WithMetrics(metrics =>
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(options =>
                        options.Endpoint = otlpEndpoint)
            );
    }
}