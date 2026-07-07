using System.Text;
using Acmp.Modules.Integrations.Webex;
using Microsoft.Extensions.Options;

namespace Acmp.Api.Endpoints;

// Authenticates the inbound Webex webhook (the only anonymous endpoint) by HMAC signature, not a user session.
// Webex signs the raw JSON body and sends the hex digest in the x-spark-signature header (lowercase under
// HTTP/2); the default algorithm is HMAC-SHA1 (SHA256/512 accepted if the webhook was created with them). We
// recompute over the RAW body and compare in constant time. On any mismatch the request is rejected 401 and
// the handler never runs. When Webex is disabled the endpoint accepts and ignores (no processing).
public sealed class WebexSignatureFilter : IEndpointFilter
{
    private const string SignatureHeader = "x-spark-signature";

    private readonly WebexOptions _options;

    public WebexSignatureFilter(IOptions<WebexOptions> options) => _options = options.Value;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;

        if (!_options.Enabled)
            return Results.Ok(); // adapter off: accept and ignore, never process

        request.EnableBuffering();
        string body;
        using (var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
            body = await reader.ReadToEndAsync();
        request.Body.Position = 0;

        var provided = request.Headers[SignatureHeader].FirstOrDefault();
        if (!WebexSignature.IsValid(_options.SignatureAlgorithm, _options.WebhookSecret, body, provided))
            return Results.Unauthorized();

        context.HttpContext.Items["webex-body"] = body;
        return await next(context);
    }
}
