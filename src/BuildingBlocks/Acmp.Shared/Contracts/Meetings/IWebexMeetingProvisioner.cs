namespace Acmp.Shared.Contracts.Meetings;

// Lets the Meetings module trigger Webex meeting auto-create without depending on the Webex integration
// (ADR-0001). The Meetings module registers a no-op default; the Webex integration overrides it (when enabled)
// with an implementation that enqueues a background create job. Fire-and-forget — it never blocks scheduling.
public interface IWebexMeetingProvisioner
{
    Task ProvisionAsync(Guid meetingPublicId, string title, DateTimeOffset start, DateTimeOffset end,
        bool isOnline, CancellationToken ct = default);
}
