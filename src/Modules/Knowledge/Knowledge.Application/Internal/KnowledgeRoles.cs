using Acmp.Shared.Authorization;

namespace Acmp.Modules.Knowledge.Application.Internal;

// The MediatR-boundary role re-check (defence in depth, guardrail 4) for the Knowledge document commands. A
// Document is not topic-scoped, so — exactly like ADRs — the Document.Manage allow-if-owner (Member/Reviewer)
// has no ownership relationship to resolve at the endpoint and Chairman/Secretary are the effective writers.
// This coarse backstop therefore lists only the full-allow roles.
internal static class KnowledgeRoles
{
    public static readonly string[] DocumentManage = { AcmpRoles.Chairman, AcmpRoles.Secretary };

    // Template.Manage's matrix row (AuthorizationRegistration.cs) grants Administrator too — and it has NO
    // allow-if-owner roles — so the backstop must include Administrator (unlike DocumentManage), or a valid
    // Administrator that passes the endpoint policy would be wrongly 403'd at the MediatR boundary.
    public static readonly string[] TemplateManage = { AcmpRoles.Chairman, AcmpRoles.Secretary, AcmpRoles.Administrator };
}
