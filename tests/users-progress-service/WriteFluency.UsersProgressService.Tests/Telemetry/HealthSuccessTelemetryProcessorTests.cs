using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Shouldly;
using WriteFluency.UsersProgressService.Telemetry;

namespace WriteFluency.UsersProgressService.Tests.Telemetry;

public class HealthSuccessTelemetryProcessorTests
{
    [Fact]
    public void Process_ShouldDropSuccessfulHealthRequest()
    {
        var sink = new CollectingTelemetryProcessor();
        var processor = new HealthSuccessTelemetryProcessor(sink);
        var telemetry = new RequestTelemetry
        {
            Name = "UsersProgressHealth",
            Url = new Uri("https://example.test/health"),
            Success = true
        };

        processor.Process(telemetry);

        sink.Items.Count.ShouldBe(0);
    }

    [Fact]
    public void Process_ShouldKeepFailedHealthRequest()
    {
        var sink = new CollectingTelemetryProcessor();
        var processor = new HealthSuccessTelemetryProcessor(sink);
        var telemetry = new RequestTelemetry
        {
            Name = "UsersProgressHealth",
            Url = new Uri("https://example.test/health"),
            Success = false
        };

        processor.Process(telemetry);

        sink.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void Process_ShouldKeepSuccessfulNonHealthRequest()
    {
        var sink = new CollectingTelemetryProcessor();
        var processor = new HealthSuccessTelemetryProcessor(sink);
        var telemetry = new RequestTelemetry
        {
            Name = "ProgressStateSave",
            Url = new Uri("https://example.test/users/progress/state"),
            Success = true
        };

        processor.Process(telemetry);

        sink.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void Process_ShouldDropInformationalHealthTrace()
    {
        var sink = new CollectingTelemetryProcessor();
        var processor = new HealthSuccessTelemetryProcessor(sink);
        var telemetry = new TraceTelemetry("Health trace", SeverityLevel.Information);
        telemetry.Properties["Category"] = "Function.UsersProgressHealth";

        processor.Process(telemetry);

        sink.Items.Count.ShouldBe(0);
    }

    [Fact]
    public void Process_ShouldKeepWarningHealthTrace()
    {
        var sink = new CollectingTelemetryProcessor();
        var processor = new HealthSuccessTelemetryProcessor(sink);
        var telemetry = new TraceTelemetry("Health warning", SeverityLevel.Warning);
        telemetry.Properties["Category"] = "Function.UsersProgressHealth";

        processor.Process(telemetry);

        sink.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void Process_ShouldDropHealthMetric()
    {
        var sink = new CollectingTelemetryProcessor();
        var processor = new HealthSuccessTelemetryProcessor(sink);
        var telemetry = new MetricTelemetry("azure.functions.health_check.reports", 1);

        processor.Process(telemetry);

        sink.Items.Count.ShouldBe(0);
    }

    private sealed class CollectingTelemetryProcessor : ITelemetryProcessor
    {
        public List<ITelemetry> Items { get; } = [];

        public void Process(ITelemetry item)
        {
            Items.Add(item);
        }
    }
}
