using Acmp.Shared.Contracts.Topics;
using NSubstitute;

namespace Acmp.Application.Tests.Shared;

/// <summary>
/// Test double for the SL-030 cross-module egress port (FR-163 / AC-114).
/// </summary>
/// <remarks>
/// ⚠ <see cref="SeesEverything"/> IS A DELIBERATE BLIND SPOT, exactly as TopicVisibilityTests says of the
/// permissive ITopicVisibility stubs. A suite wired with it stays green whether the redaction narrows
/// anything or not, because it is measuring agenda mapping, register paging or graph traversal — not
/// confidentiality. The suite that CAN see the control is ConfidentialEgressTests, and every case there is
/// written to fail if the redaction is removed.
/// </remarks>
internal static class TopicConfidentialityStub
{
    /// <summary>Hides nothing — the caller is a committee-wide reader (DEC-063 d1).</summary>
    public static ITopicConfidentiality SeesEverything() => Hiding();

    /// <summary>Hides exactly these topics from the caller.</summary>
    public static ITopicConfidentiality Hiding(params Guid[] topicIds)
    {
        var c = Substitute.For<ITopicConfidentiality>();
        c.GetHiddenTopicIdsAsync(Arg.Any<CancellationToken>()).Returns(topicIds);
        return c;
    }
}
