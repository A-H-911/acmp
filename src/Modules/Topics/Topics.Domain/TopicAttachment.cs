using Acmp.Shared.Domain.Entities;

namespace Acmp.Modules.Topics.Domain;

// File metadata for a topic attachment. Bytes live in MinIO via IFileStore; this row is the SQL-side
// metadata (AC-050). StorageKey is the object key returned by IFileStore. Size/MIME validation happens
// at the application boundary before the file is stored (AC-049).
public sealed class TopicAttachment : BaseEntity
{
    private TopicAttachment() { }

    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string UploadedBySub { get; private set; } = string.Empty;
    public string UploadedByName { get; private set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; private set; }

    internal TopicAttachment(string fileName, string contentType, long sizeBytes, string storageKey,
        string uploadedBySub, string uploadedByName, DateTimeOffset uploadedAt)
    {
        FileName = fileName.Trim();
        ContentType = contentType.Trim();
        SizeBytes = sizeBytes;
        StorageKey = storageKey.Trim();
        UploadedBySub = uploadedBySub.Trim();
        UploadedByName = uploadedByName.Trim();
        UploadedAt = uploadedAt;
    }
}
