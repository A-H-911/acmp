namespace Acmp.Modules.Integrations.Webex;

// The bits ACMP keeps after creating a Webex meeting: the meeting id (the recording-webhook correlation key)
// and the join URL (stored on the ACMP meeting).
public sealed record CreatedWebexMeeting(string Id, string JoinUrl);
