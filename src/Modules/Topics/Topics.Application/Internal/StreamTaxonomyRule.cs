using Acmp.Shared.Contracts.Membership;
using FluentValidation;

namespace Acmp.Modules.Topics.Application.Internal;

// ADR-0042 clause (7): a topic's affected streams must come from the committee's seeded taxonomy;
// free text is REJECTED. Shared by SubmitTopicValidator and UpdateTopicValidator.
//
// ⚠ IT IS SHARED BECAUSE ENFORCING IT ON SUBMIT ALONE WOULD NOT WORK. PUT /api/topics/{id} carries
// the same Streams list and writes it through Topic.AssignStreams, so a submit-only rule leaves an
// update-shaped hole that reaches the identical field — the "patched one caller" mistake this
// codebase has paid for repeatedly (see DEF-059, whose root cause is exactly that asymmetry).
//
// WHY THE CONTROL CANNOT WORK WITHOUT THIS AT ALL: the ABAC stream check intersects a topic's streams
// against Stream.Code, which is lowercase and code-shaped. While topics carried free text, a topic
// tagged "Smart Cities" could never match the stream "smart-cities" — so the check would refuse
// everything, silently and for a reason no error message would reveal (DEF-057's third finding).
internal static class StreamTaxonomyRule
{
    // Matching is case-INSENSITIVE deliberately, and it is consistent rather than lax: the ABAC
    // intersect in StreamScopeHandler uses StringComparer.OrdinalIgnoreCase, so "Core" and "core"
    // authorize identically. Validating more strictly than the control that consumes the value would
    // reject input that would have worked, which is a worse failure than accepting it.
    public static IRuleBuilderOptions<T, IReadOnlyList<string>> MustBeSeededStreams<T>(
        this IRuleBuilder<T, IReadOnlyList<string>> rule, IStreamCatalog catalog) =>
        rule.MustAsync(async (streams, ct) =>
            {
                // An empty list is NOT this rule's business — SubmitTopicValidator requires NotEmpty
                // separately, and on update the empty case is DEF-059's, decided in DEC-043 and
                // landing with steps 6-7. Failing it here too would report a confusing second error.
                if (streams is null || streams.Count == 0) return true;

                var seeded = await catalog.GetAssignableStreamCodesAsync(ct);
                return streams.All(s => seeded.Contains(s, StringComparer.OrdinalIgnoreCase));
            })
            .WithMessage("Affected streams must be chosen from the committee's streams.");
}
