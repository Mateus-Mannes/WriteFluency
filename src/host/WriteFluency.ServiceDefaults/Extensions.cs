using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// Adds common .NET Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var resourceName = builder.Configuration["RESOURCE_NAME"] ?? builder.Environment.ApplicationName;
        var resourceAttributes = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(resourceName))
        {
            // Create a dictionary of resource attributes.
            resourceAttributes = new Dictionary<string, object> {
                { "service.name", resourceName! }};
        }
        // Create a resource builder.
        var resourceBuilder = ResourceBuilder.CreateDefault().AddAttributes(resourceAttributes);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
#if !DEBUG 
            logging.SetResourceBuilder(resourceBuilder);
            logging.AddAzureMonitorLogExporter();
#endif
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
#if !DEBUG
                metrics.AddMeter(
                    "Microsoft.AspNetCore.Hosting",
                    "System.Net.Http",
                    "System.Runtime",
                    "System.Data.SqlClient",
                    "Npgsql"
                );

                metrics.SetResourceBuilder(resourceBuilder);
                metrics.AddAzureMonitorMetricExporter();

                metrics.AddView("http.server.request.duration", new MetricStreamConfiguration
                {
                    TagKeys = new[] { "http.request.method", "http.route", "http.response.status_code" }
                });
                metrics.AddView("http.client.request.duration", new MetricStreamConfiguration
                {
                    TagKeys = new[] { "http.request.method", "http.response.status_code", "server.address" }
                });
                metrics.AddView("db.client.commands.duration", new MetricStreamConfiguration
                {
                    TagKeys = new[] { "db.system" }
                });

                var buckets = new double[] { 5,10,25,50,100,250,500,1000,2500,5000 };
                metrics.AddView("http.server.request.duration", new ExplicitBucketHistogramConfiguration { Boundaries = buckets });
                metrics.AddView("http.client.request.duration", new ExplicitBucketHistogramConfiguration { Boundaries = buckets });
#endif
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                    )
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
#if !DEBUG
                tracing.SetResourceBuilder(resourceBuilder);
                tracing.AddAzureMonitorTraceExporter();
#endif
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddRequestTimeouts(
            configure: static timeouts =>
                timeouts.AddPolicy("HealthChecks", TimeSpan.FromSeconds(5)));

        builder.Services.AddOutputCache(
            configureOptions: static caching =>
                caching.AddPolicy("HealthChecks",
                build: static policy => policy.Expire(TimeSpan.FromSeconds(10))));

        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static TBuilder AddMinioHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks().AddUrlGroup(options =>
            {
                var uri = new Uri(builder.Configuration.GetConnectionString("wf-minio")!.Split(";")[0].Replace("Endpoint=", ""));
                options.AddUri(new Uri(uri, "/minio/health/live"), setup => setup.ExpectHttpCode(200));
                options.AddUri(new Uri(uri, "/minio/health/cluster"), setup => setup.ExpectHttpCode(200));
                options.AddUri(new Uri(uri, "/minio/health/cluster/read"), setup => setup.ExpectHttpCode(200));
            }, "minio_health", tags: new[] { "ready", "live" });
        return builder;
    }    

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        var healthChecks = app.MapGroup("");

        healthChecks
            .CacheOutput("HealthChecks")
            .WithRequestTimeout("HealthChecks");

        // All health checks must pass for app to be
        // considered ready to accept traffic after starting
        healthChecks.MapHealthChecks("/health");

        // Only health checks tagged with the "live" tag
        // must pass for app to be considered alive
        healthChecks.MapHealthChecks("/alive", new()
        {
            Predicate = static r => r.Tags.Contains("live")
        });

        return app;
    }
}
