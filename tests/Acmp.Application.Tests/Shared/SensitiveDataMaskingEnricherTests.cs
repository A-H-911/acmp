using Acmp.Shared.Infrastructure.Observability;
using FluentAssertions;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Acmp.Application.Tests.Shared;

// C-PRIV-01/02: sensitive structured properties are redacted before a sink sees them; the pseudonymous
// UserId and ordinary properties pass through.
public class SensitiveDataMaskingEnricherTests
{
    private sealed class CaptureSink : ILogEventSink
    {
        public LogEvent? Last { get; private set; }
        public void Emit(LogEvent logEvent) => Last = logEvent;
    }

    [Fact]
    public void Masks_sensitive_properties_and_keeps_the_rest()
    {
        var sink = new CaptureSink();
        using var logger = new LoggerConfiguration()
            .Enrich.With(new SensitiveDataMaskingEnricher())
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("event {UserId} {Email} {BotToken} {ConnectionString} {Action}",
            "kc-omar", "omar@acmp.gov", "bot-secret-123", "Server=sql;Password=p", "SignIn");

        var props = sink.Last!.Properties;
        props["Email"].ToString().Should().Contain(SensitiveDataMaskingEnricher.Redacted).And.NotContain("omar@acmp.gov");
        props["BotToken"].ToString().Should().Contain(SensitiveDataMaskingEnricher.Redacted).And.NotContain("bot-secret-123");
        props["ConnectionString"].ToString().Should().Contain(SensitiveDataMaskingEnricher.Redacted);
        props["UserId"].ToString().Should().Contain("kc-omar");   // pseudonymous id preserved
        props["Action"].ToString().Should().Contain("SignIn");    // ordinary property preserved
    }
}
