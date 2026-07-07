using Acmp.Shared.Contracts.Meetings;

namespace Acmp.Modules.Meetings.Infrastructure.Directory;

// The default meeting-provision seam: does nothing. Registered by the Meetings module so ScheduleMeeting can
// always resolve IWebexMeetingProvisioner. When the Webex adapter is enabled it registers its real provisioner
// AFTER this one (composition order), so the real one wins; when Webex is off, this no-op keeps scheduling
// working with zero Webex coupling.
public sealed class NullWebexMeetingProvisioner : IWebexMeetingProvisioner
{
    public Task ProvisionAsync(Guid meetingPublicId, string title, DateTimeOffset start, DateTimeOffset end,
        bool isOnline, CancellationToken ct = default) => Task.CompletedTask;
}
