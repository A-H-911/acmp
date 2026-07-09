using Acmp.Modules.Integrations.Webex;
using Acmp.Modules.Integrations.Webex.Oauth;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Acmp.Application.Tests.Webex;

// The token service hands out a valid access token: returns the stored one while fresh, transparently
// refreshes (and re-persists) when expired, and returns null when there is nothing usable.
public class WebexTokenServiceTests
{
    private static WebexDbContext Db() =>
        new(new DbContextOptionsBuilder<WebexDbContext>().UseInMemoryDatabase("webex-" + Guid.NewGuid()).Options);

    private static WebexTokenProtector Protector() =>
        new(Options.Create(new WebexOptions { TokenEncryptionKey = "k" }));

    private static IClock ClockAt(DateTimeOffset now)
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(now);
        return c;
    }

    private static readonly DateTimeOffset T0 = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Returns_null_when_no_token_is_stored()
    {
        var svc = new WebexTokenService(Db(), Protector(), Substitute.For<IWebexOAuthClient>(),
            ClockAt(T0), NullLogger<WebexTokenService>.Instance);

        (await svc.GetValidAccessTokenAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Returns_the_stored_access_token_while_fresh()
    {
        var db = Db();
        var svc = new WebexTokenService(db, Protector(), Substitute.For<IWebexOAuthClient>(),
            ClockAt(T0), NullLogger<WebexTokenService>.Instance);
        await svc.StoreFromExchangeAsync(new WebexTokenResponse { AccessToken = "acc-1", RefreshToken = "ref-1", ExpiresIn = 3600 });

        (await svc.GetValidAccessTokenAsync()).Should().Be("acc-1");
    }

    [Fact]
    public async Task Refreshes_and_repersists_when_the_access_token_has_expired()
    {
        var db = Db();
        var oauth = Substitute.For<IWebexOAuthClient>();
        oauth.RefreshAsync("ref-1", Arg.Any<CancellationToken>())
            .Returns(new WebexTokenResponse { AccessToken = "acc-2", RefreshToken = "ref-2", ExpiresIn = 3600 });
        var clock = ClockAt(T0);
        var svc = new WebexTokenService(db, Protector(), oauth, clock, NullLogger<WebexTokenService>.Instance);
        await svc.StoreFromExchangeAsync(new WebexTokenResponse { AccessToken = "acc-1", RefreshToken = "ref-1", ExpiresIn = 60 });

        clock.UtcNow.Returns(T0.AddHours(1)); // now expired

        (await svc.GetValidAccessTokenAsync()).Should().Be("acc-2");
        await oauth.Received(1).RefreshAsync("ref-1", Arg.Any<CancellationToken>());
        // The rotated refresh token is persisted (a subsequent refresh would use ref-2).
        var stored = await db.Tokens.SingleAsync();
        stored.Should().NotBeNull();
    }

    [Fact]
    public async Task Returns_null_when_a_refresh_yields_no_usable_tokens()
    {
        var db = Db();
        var oauth = Substitute.For<IWebexOAuthClient>();
        oauth.RefreshAsync("ref-1", Arg.Any<CancellationToken>()).Returns((WebexTokenResponse?)null);
        var clock = ClockAt(T0);
        var svc = new WebexTokenService(db, Protector(), oauth, clock, NullLogger<WebexTokenService>.Instance);
        await svc.StoreFromExchangeAsync(new WebexTokenResponse { AccessToken = "acc-1", RefreshToken = "ref-1", ExpiresIn = 60 });

        clock.UtcNow.Returns(T0.AddHours(1)); // now expired

        (await svc.GetValidAccessTokenAsync()).Should().BeNull();
    }

    [Theory]
    [InlineData(null, "ref-1")]
    [InlineData("acc-1", null)]
    public async Task StoreFromExchange_rejects_a_response_missing_the_access_or_refresh_token(string? access, string? refresh)
    {
        var svc = new WebexTokenService(Db(), Protector(), Substitute.For<IWebexOAuthClient>(),
            ClockAt(T0), NullLogger<WebexTokenService>.Instance);

        var act = () => svc.StoreFromExchangeAsync(new WebexTokenResponse { AccessToken = access, RefreshToken = refresh, ExpiresIn = 3600 });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task HasToken_reflects_whether_a_token_is_stored()
    {
        var db = Db();
        var svc = new WebexTokenService(db, Protector(), Substitute.For<IWebexOAuthClient>(),
            ClockAt(T0), NullLogger<WebexTokenService>.Instance);

        (await svc.HasTokenAsync()).Should().BeFalse();
        await svc.StoreFromExchangeAsync(new WebexTokenResponse { AccessToken = "acc-1", RefreshToken = "ref-1", ExpiresIn = 3600 });
        (await svc.HasTokenAsync()).Should().BeTrue();
    }
}
