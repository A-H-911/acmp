namespace Acmp.Modules.Integrations.Webex;

// Strongly-typed Webex configuration (bound from the "Webex" section). Every value is secret-or-env
// sourced — appsettings ships CHANGE_ME_IN_ENV placeholders (INV-003, CON-001). When Enabled is false the
// whole adapter is left unregistered (AC-071): in-app stays the sole channel and no outbound call is made.
public sealed class WebexOptions
{
    public const string SectionName = "Webex";

    // Master kill-switch. false => the adapter is not wired at all (air-gapped / v1 posture).
    public bool Enabled { get; init; }

    // Bot identity for outbound space notifications (ADR-0005; webex-feasibility.md §4).
    public string BotToken { get; init; } = string.Empty;
    public string SpaceId { get; init; } = string.Empty;          // committee Webex space (roomId)

    public string ApiBaseUrl { get; init; } = "https://webexapis.com/v1";

    // Inbound webhook verification (webex-feasibility.md §3.1). Default algorithm is HMAC-SHA1 (Webex
    // default for X-Spark-Signature); SHA256/SHA512 are also accepted if the webhook was created with them.
    public string WebhookSecret { get; init; } = string.Empty;
    public string SignatureAlgorithm { get; init; } = "HMACSHA1"; // HMACSHA1 | HMACSHA256 | HMACSHA512
    public string WebhookPublicUrl { get; init; } = string.Empty; // ngrok/reverse-proxy URL used at registration

    // Absolute base for the "Open in ACMP" card button (card deep links are stored relative).
    public string AcmpBaseUrl { get; init; } = string.Empty;
    public string DefaultLanguage { get; init; } = "en";         // card render language: en | ar

    // OAuth (secretary user token) for meeting auto-create — a bot cannot host a meeting (feasibility §4).
    public string OAuthClientId { get; init; } = string.Empty;
    public string OAuthClientSecret { get; init; } = string.Empty;
    public string OAuthRedirectUri { get; init; } = string.Empty;
    public string OAuthScopes { get; init; } = "spark:messages_write meeting:schedules_write meeting:recordings_read";

    // Symmetric key (base64) encrypting the persisted OAuth refresh/access tokens at rest.
    public string TokenEncryptionKey { get; init; } = string.Empty;
}
