namespace Acmp.Modules.Membership.Domain;

// Join row: a member's assignment to a stream (docs/10 §E.1). Owned by CommitteeMember; the
// (member, stream) pair is the composite key.
public sealed class MemberStreamAssignment
{
    private MemberStreamAssignment() { }

    public MemberStreamAssignment(long streamId) => StreamId = streamId;

    public long CommitteeMemberId { get; private set; }
    public long StreamId { get; private set; }
}
