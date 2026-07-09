using Acmp.Shared.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Acmp.Modules.Integrations.Webex.Oauth;

// Manages the single persisted OAuth token: stores it (encrypted) after the consent exchange, and hands out a
// VALID access token — transparently refreshing (and re-persisting the rotated refresh token) when it has
// expired. Returns null when no token exists or a refresh fails, so callers degrade gracefully (AC-072).
public interface IWebexTokenService
{
    Task StoreFromExchangeAsync(WebexTokenResponse token, CancellationToken ct = default);

    Task<string?> GetValidAccessTokenAsync(CancellationToken ct = default);

    Task<bool> HasTokenAsync(CancellationToken ct = default);
}

public sealed class WebexTokenService : IWebexTokenService
{
    private static readonly TimeSpan ExpiryskEw = TimeSpan.FromMinutes(2);

    private readonly WebexDbContext _db;
    private readonly WebexTokenProtector _protector;
    private readonly IWebexOAuthClient _oauth;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ILogger<WebexTokenService> _logger;

    public WebexTokenService(WebexDbContext db, WebexTokenProtector protector, IWebexOAuthClient oauth,
        IClock clock, IAuditSink audit, ILogger<WebexTokenService> logger)
    {
        _db = db;
        _protector = protector;
        _oauth = oauth;
        _clock = clock;
        _audit = audit;
        _logger = logger;
    }

    public Task StoreFromExchangeAsync(WebexTokenResponse token, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(token.AccessToken) || string.IsNullOrEmpty(token.RefreshToken))
            throw new InvalidOperationException("Webex token response is missing the access or refresh token.");
        return UpsertAsync(token, ct);
    }

    public async Task<string?> GetValidAccessTokenAsync(CancellationToken ct = default)
    {
        var row = await _db.Tokens.FirstOrDefaultAsync(t => t.Id == WebexToken.SingletonId, ct);
        if (row is null)
            return null;

        if (row.AccessTokenExpiresAt - ExpiryskEw > _clock.UtcNow)
            return _protector.Unprotect(row.AccessTokenCipher);

        WebexTokenResponse? refreshed;
        try
        {
            refreshed = await _oauth.RefreshAsync(_protector.Unprotect(row.RefreshTokenCipher), ct);
        }
        catch (WebexApiException ex) when (ex.StatusCode is 400 or 401)
        {
            // The stored refresh token was rejected (revoked / re-consent required). Degrade gracefully per the
            // service contract + AC-072: the caller (meeting-create / recording jobs) no-ops instead of throwing
            // and dead-lettering. Transient failures (429 / 5xx) still bubble so Hangfire retries them.
            _logger.LogWarning(ex,
                "Webex refresh token rejected (HTTP {Status}); integration unavailable until re-consent", ex.StatusCode);
            return null;
        }

        if (refreshed?.AccessToken is null || refreshed.RefreshToken is null)
        {
            _logger.LogWarning("Webex token refresh returned no usable tokens; meeting integration is unavailable");
            return null;
        }

        await UpsertAsync(refreshed, ct);
        // m5 (INV-005): rotating a stored security credential is a state change — audit it. The initial link is
        // audited at the OAuth callback; this covers the transparent background refresh (self-attributed system actor).
        await _audit.EmitAsync("Webex.OAuthTokenRefreshed", "system:webex", new { rotatedAt = _clock.UtcNow }, ct);
        return refreshed.AccessToken;
    }

    public Task<bool> HasTokenAsync(CancellationToken ct = default) =>
        _db.Tokens.AnyAsync(t => t.Id == WebexToken.SingletonId, ct);

    private async Task UpsertAsync(WebexTokenResponse token, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var row = await _db.Tokens.FirstOrDefaultAsync(t => t.Id == WebexToken.SingletonId, ct);
        if (row is null)
        {
            row = new WebexToken();
            _db.Tokens.Add(row);
        }

        row.AccessTokenCipher = _protector.Protect(token.AccessToken!);
        row.RefreshTokenCipher = _protector.Protect(token.RefreshToken!);
        row.AccessTokenExpiresAt = now.AddSeconds(token.ExpiresIn);
        row.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
    }
}
