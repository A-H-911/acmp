using Acmp.Shared.Contracts.Meetings;
using Microsoft.Extensions.Options;

namespace Acmp.Modules.Integrations.Webex;

// The enabled implementation of the meeting-provision seam: enqueues a background create job for online
// meetings. Only online meetings are provisioned (in-person meetings need no Webex meeting). Enqueue only —
// the actual Webex call happens on the worker, keeping scheduling fast and rate-limit safe.
public sealed class WebexMeetingProvisioner : IWebexMeetingProvisioner
{
    private readonly WebexOptions _options;
    private readonly IWebexJobScheduler _scheduler;

    public WebexMeetingProvisioner(IOptions<WebexOptions> options, IWebexJobScheduler scheduler)
    {
        _options = options.Value;
        _scheduler = scheduler;
    }

    public Task ProvisionAsync(Guid meetingPublicId, string title, DateTimeOffset start, DateTimeOffset end,
        bool isOnline, CancellationToken ct = default)
    {
        if (_options.Enabled && isOnline)
            _scheduler.Enqueue<WebexMeetingCreateJob>(
                job => job.CreateAsync(meetingPublicId, title, start, end, CancellationToken.None));

        return Task.CompletedTask;
    }
}
