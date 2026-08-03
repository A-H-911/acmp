namespace Acmp.Shared.Application.Abstractions;

// Object-storage bucket names (DEF-015). These were `public const string Bucket` on four handlers, so every
// environment wrote to the same two buckets — UAT and production would have shared one, breaking per-env
// isolation (AC-083). Configurable per environment instead.
//
// Two names, not one: on-prem/dev keeps the historical MinIO split (recordings + topic attachments) so no
// existing object has to move, while the cloud stack points BOTH at the single per-environment bucket that
// deploy/aws/02-s3.sh creates ("meeting recordings + topic attachments"). Object keys are already namespaced
// by meeting key / topic id + GUID, so sharing one bucket cannot collide.
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string RecordingsBucket { get; set; } = "acmp-recordings";

    public string AttachmentsBucket { get; set; } = "acmp-topics";
}
