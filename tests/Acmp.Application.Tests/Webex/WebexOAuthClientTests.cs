using System.Net;
using System.Text;
using Acmp.Modules.Integrations.Webex;
using Acmp.Modules.Integrations.Webex.Oauth;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Acmp.Application.Tests.Webex;

// The concrete OAuth client posts the form grant and parses the snake_case token response; non-success throws.
public class WebexOAuthClientTests
{
    private static WebexOAuthClient Client(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        new(new HttpClient(new StubHandler(responder)) { BaseAddress = new Uri("https://webexapis.com/v1/") },
            Options.Create(new WebexOptions { OAuthClientId = "cid", OAuthClientSecret = "sec", OAuthRedirectUri = "uri" }));

    private static HttpResponseMessage Token() => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            "{\"access_token\":\"acc\",\"refresh_token\":\"ref\",\"expires_in\":1209600}", Encoding.UTF8, "application/json"),
    };

    [Fact]
    public async Task Exchange_code_parses_the_token_response()
    {
        var token = await Client(_ => Token()).ExchangeCodeAsync("the-code");
        token!.AccessToken.Should().Be("acc");
        token.RefreshToken.Should().Be("ref");
        token.ExpiresIn.Should().Be(1209600);
    }

    [Fact]
    public async Task Refresh_parses_the_token_response()
    {
        var token = await Client(_ => Token()).RefreshAsync("old-refresh");
        token!.AccessToken.Should().Be("acc");
    }

    [Fact]
    public async Task Throws_on_a_non_success_response()
    {
        var client = Client(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("invalid_grant"),
        });
        await client.Invoking(c => c.ExchangeCodeAsync("bad")).Should().ThrowAsync<WebexApiException>();
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(_responder(request));
    }
}
