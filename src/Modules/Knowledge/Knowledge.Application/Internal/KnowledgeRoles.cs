using Acmp.Shared.Authorization;

namespace Acmp.Modules.Knowledge.Application.Internal;

// The MediatR-boundary role re-check (defence in depth, guardrail 4) for the Knowledge document commands. A
// Document is not topic-scoped, so — exactly like ADRs — the Document.Manage allow-if-owner (Member/Reviewer)
// has no ownership relationship to resolve at the endpoint and Chairman/Secretary are the effective writers.
// This coarse backstop therefore lists only the full-allow roles.
internal static class KnowledgeRoles
{
    public static readonly string[] DocumentManage = { AcmpRoles.Chairman, AcmpRoles.Secretary };
}
