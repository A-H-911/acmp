using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Topics;
using MediatR;

namespace Acmp.Modules.Topics.Application.Features.GetTopicAttachmentUrl;

// WBS-40.12 / AC-163 (DEC-190): the committee half of attachment download. Anyone who can read the topic
// may open its attachments (a1): the empty role list is the topic-detail read set, guests never reach
// /api/topics (GuestSurfaceMiddleware), and ITopicReader.GetMaterialDownloadUrlAsync (AC-164) - the same scoped reader the guest
// path uses - scopes the lookup by the caller's topic visibility, so FR-163 narrows a Restricted topic and
// an attachment outside it reads as "no such attachment". Returns null for every refusal (the endpoint's 404).
public sealed record GetTopicAttachmentUrlQuery(Guid TopicId, Guid AttachmentId) : IRequest<string?>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetTopicAttachmentUrlHandler : IRequestHandler<GetTopicAttachmentUrlQuery, string?>
{
    private readonly ITopicReader _topics;
    private readonly IAuditSink _audit;

    public GetTopicAttachmentUrlHandler(ITopicReader topics, IAuditSink audit)
    {
        _topics = topics;
        _audit = audit;
    }

    public async Task<string?> Handle(GetTopicAttachmentUrlQuery request, CancellationToken ct)
    {
        var url = await _topics.GetMaterialDownloadUrlAsync(request.TopicId, request.AttachmentId, ct);
        if (url is null) return null;

        // Audited AFTER the URL is minted, like Meetings.RecordingAccessed: the row means "a capability was
        // handed out". The URL itself is never recorded (NFR-027); the attachment id answers who opened what.
        await _audit.EmitEnrichedAsync("Topics.AttachmentAccessed", "TopicAttachment", request.AttachmentId.ToString(), ct: ct);
        return url;
    }
}
