using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Contracts;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Modules.Topics.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Acmp.Modules.Topics.Application.Features.AttachFileToTopic;

// AC-049/050: attach a file to a topic. Size/MIME validated against TopicAttachmentOptions (400 with a
// clear message; no partial store). Bytes go to MinIO via IFileStore; metadata to SQL via the aggregate.
// The submitter may attach to their own topic; otherwise an editor (Owner/Secretary) via ABAC.
public sealed record AttachFileToTopicCommand(
    Guid TopicId, string FileName, string ContentType, long SizeBytes, Stream Content) : IRequest<TopicAttachmentDto>;

public sealed class AttachFileToTopicValidator : AbstractValidator<AttachFileToTopicCommand>
{
    public AttachFileToTopicValidator(IOptions<TopicAttachmentOptions> options)
    {
        var o = options.Value;
        RuleFor(x => x.TopicId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.SizeBytes)
            .GreaterThan(0)
            .LessThanOrEqualTo(o.MaxSizeBytes)
            .WithErrorCode("FILE_TOO_LARGE")   // BL-016: the SPA localizes by code (it owns the MB limit for display)
            .WithMessage($"File exceeds the maximum allowed size of {o.MaxSizeBytes / (1024 * 1024)} MB.");
        RuleFor(x => x.ContentType)
            .Must(ct => o.AllowedContentTypes.Contains(ct, StringComparer.OrdinalIgnoreCase))
            .WithErrorCode("FILE_TYPE_NOT_ALLOWED")
            .WithMessage(x => $"File type '{x.ContentType}' is not allowed.");
    }
}

public sealed class AttachFileToTopicHandler : IRequestHandler<AttachFileToTopicCommand, TopicAttachmentDto>
{
    public const string Bucket = "acmp-topics";

    private readonly ITopicsDbContext _db;
    private readonly IResourceAuthorizer _authz;
    private readonly IFileStore _files;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly IFileContentInspector _inspector;

    public AttachFileToTopicHandler(ITopicsDbContext db, IResourceAuthorizer authz, IFileStore files,
        ICurrentUser user, IClock clock, IAuditSink audit, IFileContentInspector inspector)
    {
        _db = db;
        _authz = authz;
        _files = files;
        _user = user;
        _clock = clock;
        _audit = audit;
        _inspector = inspector;
    }

    public async Task<TopicAttachmentDto> Handle(AttachFileToTopicCommand request, CancellationToken ct)
    {
        var topic = await _db.Topics.Include(t => t.Attachments).FirstOrDefaultAsync(t => t.PublicId == request.TopicId, ct)
            ?? throw new KeyNotFoundException("Topic not found.");

        var (sub, name) = CurrentActor.Of(_user);
        if (topic.SubmittedBySub != sub)
            await _authz.EnsureAsync(topic, Policies.TopicEdit, ct);

        // C-FILE-01: confirm the actual bytes match the allow-listed declared type before storing (fail-closed).
        if (!_inspector.ContentMatchesDeclared(request.Content, request.ContentType))
            throw new ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure(nameof(request.ContentType),
                    $"File content does not match its declared type '{request.ContentType}'.")
                { ErrorCode = "FILE_CONTENT_MISMATCH" },   // BL-016
            });

        // Object key is server-derived (GUID + a content-type extension) — NEVER the raw client filename, which
        // could carry path/encoding tricks or break the presigned-URL signature. The original name is kept as
        // display metadata (attachment.FileName) only.
        var objectName = $"{topic.PublicId}/{Guid.NewGuid()}{ExtensionFor(request.ContentType)}";
        var storageKey = await _files.UploadAsync(Bucket, objectName, request.Content, request.ContentType, ct);

        var attachment = topic.AddAttachment(request.FileName, request.ContentType, request.SizeBytes,
            storageKey, sub, name, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Topics.DocumentAttached", nameof(Topic), topic.PublicId.ToString(), ct: ct);

        return new TopicAttachmentDto(attachment.PublicId, attachment.FileName, attachment.ContentType,
            attachment.SizeBytes, attachment.UploadedByName, attachment.UploadedAt);
    }

    // Extension for the stored object, from the already-allow-listed content type (validator guarantees one
    // of these; the fallback is defensive only).
    private static string ExtensionFor(string contentType) => contentType.ToLowerInvariant() switch
    {
        "application/pdf" => ".pdf",
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/svg+xml" => ".svg",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
        _ => ".bin",
    };
}
