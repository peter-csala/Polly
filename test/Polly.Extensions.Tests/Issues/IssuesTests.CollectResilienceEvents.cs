using Polly.Telemetry;

namespace Polly.Extensions.Tests.Issues;

public partial class IssuesTests
{
    private static readonly ResiliencePropertyKey<List<ResilienceEvent>> ResilienceEvents = new("MyFeature.ResilienceEvents");

    [Fact]
    public void CollectResilienceEvents_UsingTelemetryListener()
    {
        // arrange
        var telemetryOptions = new TelemetryOptions();
        telemetryOptions.TelemetryListeners.Add(new ResilienceEventListener());

        // prepare the resilience context
        var context = ResilienceContextPool.Shared.Get();
        var events = new List<ResilienceEvent>();
        context.Properties.Set(ResilienceEvents, events);

        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new()
            {
                Delay = TimeSpan.Zero,
                ShouldHandle = _ => PredicateResult.True
            })
            .ConfigureTelemetry(telemetryOptions)
            .Build();

        // act
        pipeline.Execute(_ => { }, context);

        // assert
        events.Where(e => e.EventName == "ExecutionAttempt").Should().HaveCount(4);
        events.Where(e => e.EventName == "OnRetry").Should().HaveCount(3);
        events.Should().HaveCount(7);
    }

    private class ResilienceEventListener : TelemetryListener
    {
        public override void Write<TResult, TArgs>(in TelemetryEventArguments<TResult, TArgs> args)
        {
            if (args.Event.Severity >= ResilienceEventSeverity.Warning && args.Context.Properties.TryGetValue(ResilienceEvents, out var events))
            {
                events.Add(args.Event);
            }
        }
    }
}
