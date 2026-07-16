using Serilog.Core;
using Serilog.Events;

namespace Acmp.Shared.Infrastructure.Observability;

// C-PRIV-01/02 (P16-B4): a Serilog enricher that masks known-sensitive structured properties before any sink
// (Console/Seq) sees them, so logs + traces carry a pseudonymous UserId only — never emails, secrets, tokens,
// or pre-signed URLs (NFR-028). Matching is by property NAME (case-insensitive); the value is replaced with a
// fixed redaction marker. Defence-in-depth alongside the convention of never logging raw PII.
public sealed class SensitiveDataMaskingEnricher : ILogEventEnricher
{
    public const string Redacted = "***";

    // Property names whose VALUE must never reach a sink. Kept deliberately narrow + high-signal (secrets,
    // credentials, contact PII, signed URLs) so ordinary structured logging stays useful.
    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "email", "password", "passwd", "token", "accesstoken", "refreshtoken", "bottoken", "secret",
        "clientsecret", "webhooksecret", "accesskey", "secretkey", "apikey", "authorization",
        "presignedurl", "signedurl", "tokenencryptionkey", "connectionstring",
    };

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // Snapshot the names first — AddOrUpdateProperty mutates the properties collection.
        foreach (var name in logEvent.Properties.Keys.ToArray())
        {
            if (SensitiveNames.Contains(name))
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(name, Redacted));
        }
    }
}
