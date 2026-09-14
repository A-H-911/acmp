using Acmp.Modules.Meetings.Application;
using Acmp.Modules.Topics.Application;
using Microsoft.Extensions.Options;

namespace Acmp.Api.Endpoints;

// AC-169 / DEF-180: the upload limits the SPA states come FROM the options the validators enforce, so the page
// cannot drift from the server again (DEF-167: the hint said 50 MB after the cap became 100 MB; DW-009 before
// it). Read-only, any signed-in user; the values are configuration, not data.
public static class UploadEndpoints
{
    public sealed record UploadLimits(
        long AttachmentMaxBytes, IReadOnlyCollection<string> AttachmentContentTypes,
        long RecordingMaxBytes, IReadOnlyCollection<string> RecordingContentTypes);

    public static IEndpointRouteBuilder MapUploadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/uploads/limits", (IOptions<TopicAttachmentOptions> attachments, IOptions<MeetingRecordingOptions> recordings) =>
            Results.Ok(new UploadLimits(
                attachments.Value.MaxSizeBytes, attachments.Value.AllowedContentTypes,
                recordings.Value.MaxSizeBytes, recordings.Value.AllowedContentTypes)))
            .WithTags("Uploads")
            .RequireAuthorization();
        return app;
    }
}
