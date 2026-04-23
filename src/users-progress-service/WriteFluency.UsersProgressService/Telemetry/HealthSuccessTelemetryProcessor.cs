using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace WriteFluency.UsersProgressService.Telemetry;

public sealed class HealthSuccessTelemetryProcessor : ITelemetryProcessor
{
    private const string HealthPath = "/health";
    private const string HealthOperationName = "UsersProgressHealth";
    private const string HealthFunctionCategory = "Function.UsersProgressHealth";
    private readonly ITelemetryProcessor _next;

    public HealthSuccessTelemetryProcessor(ITelemetryProcessor next)
    {
        _next = next;
    }

    public void Process(ITelemetry item)
    {
        if (ShouldDrop(item))
        {
            return;
        }

        _next.Process(item);
    }

    private static bool ShouldDrop(ITelemetry item)
    {
        return item switch
        {
            RequestTelemetry request => IsSuccessfulHealthRequest(request),
            TraceTelemetry trace => IsLowSeverityHealthTrace(trace),
            MetricTelemetry metric => IsHealthMetric(metric),
            _ => false
        };
    }

    private static bool IsSuccessfulHealthRequest(RequestTelemetry request)
    {
        if (request.Success != true)
        {
            return false;
        }

        if (string.Equals(request.Name, HealthOperationName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return request.Url is not null
            && string.Equals(request.Url.AbsolutePath, HealthPath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLowSeverityHealthTrace(TraceTelemetry trace)
    {
        if (trace.SeverityLevel is SeverityLevel.Warning or SeverityLevel.Error or SeverityLevel.Critical)
        {
            return false;
        }

        if (!trace.Properties.TryGetValue("Category", out var category))
        {
            return false;
        }

        return string.Equals(category, HealthFunctionCategory, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHealthMetric(MetricTelemetry metric)
    {
        return metric.Name.Contains("health_check", StringComparison.OrdinalIgnoreCase)
            || metric.Name.Contains(HealthOperationName, StringComparison.OrdinalIgnoreCase);
    }
}
