using Acmp.Shared.Authorization.Abac;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static Acmp.Shared.Authorization.AcmpRoles;

namespace Acmp.Shared.Authorization;

// Composition root for ACMP authorization: the Keycloak role-claim mapper, the ABAC handlers, and
// the named-policy registry encoding the docs/10 §C capability matrix. Each policy is a single
// CapabilityRequirement (full-allow roles + allow-if-owner roles); Deny is the absence of both, so
// Administrator's exclusion from committee content (SoD-5) is structural — it is simply never
// listed on a content row.
public static class AuthorizationRegistration
{
    // Row = (policy, full-Allow roles, Allow-if-owner roles). Transcribed from docs/10 §C.
    // NOTE: this is the *registration* encoding. The permission-matrix test encodes the expected
    // A/AiO/D cells INDEPENDENTLY and asserts the registered policies match (no shared table).
    private static readonly (string Policy, string[] Allow, string[] Owner)[] Matrix =
    {
        (Policies.TopicSubmit,          new[] { Chairman, Secretary, Member, Reviewer, Submitter }, Array.Empty<string>()),
        (Policies.TopicTriage,          new[] { Chairman, Secretary }, Array.Empty<string>()),
        (Policies.TopicEdit,            new[] { Chairman, Secretary }, new[] { Member, Reviewer, Submitter }),
        (Policies.BacklogPrioritize,    new[] { Chairman, Secretary }, Array.Empty<string>()),
        (Policies.AgendaPublish,        new[] { Chairman, Secretary }, Array.Empty<string>()),
        (Policies.MeetingSchedule,      new[] { Chairman, Secretary }, Array.Empty<string>()),
        (Policies.AttendanceRecord,     new[] { Chairman, Secretary }, Array.Empty<string>()),
        (Policies.MinutesCapture,       new[] { Chairman, Secretary }, Array.Empty<string>()),
        (Policies.MinutesApprove,       new[] { Chairman, Secretary }, Array.Empty<string>()),
        (Policies.VoteManage,           new[] { Chairman, Secretary }, Array.Empty<string>()),
        (Policies.VoteCast,             new[] { Chairman, Member }, Array.Empty<string>()),
        (Policies.DecisionRecord,       new[] { Chairman, Secretary }, Array.Empty<string>()),
        (Policies.DecisionChairApprove, new[] { Chairman }, Array.Empty<string>()),
        (Policies.ActionCreate,         new[] { Chairman, Secretary }, new[] { Member }),
        (Policies.ActionVerify,         new[] { Chairman, Secretary }, new[] { Member }),
        (Policies.RiskManage,           new[] { Chairman, Secretary }, new[] { Member, Reviewer }),
        (Policies.RiskAccept,           new[] { Chairman, Secretary }, Array.Empty<string>()),
        (Policies.DependencyCreate,     new[] { Chairman, Secretary }, new[] { Member, Reviewer }),
        (Policies.TraceabilityLink,     new[] { Chairman, Secretary }, Array.Empty<string>()),
        (Policies.AdrCreate,            new[] { Chairman, Secretary }, new[] { Member, Reviewer }),
        (Policies.AdrApprove,           new[] { Chairman, Secretary }, Array.Empty<string>()),
        (Policies.AdrSupersede,         new[] { Chairman, Secretary }, Array.Empty<string>()),
        (Policies.InvariantCreate,      new[] { Chairman, Secretary }, new[] { Member, Reviewer }),
        (Policies.InvariantApprove,     new[] { Chairman, Secretary }, Array.Empty<string>()),
        (Policies.TemplateManage,       new[] { Chairman, Secretary, Administrator }, Array.Empty<string>()),
        (Policies.DocumentManage,       new[] { Chairman, Secretary }, new[] { Member, Reviewer }),
        (Policies.DiagramAttach,        new[] { Chairman, Secretary }, new[] { Member, Reviewer, Submitter, Guest }),
        (Policies.ResearchManage,       new[] { Chairman, Secretary }, new[] { Member, Reviewer }),
        (Policies.AdminUsers,           new[] { Administrator }, Array.Empty<string>()),
        (Policies.AuthDelegate,         new[] { Chairman, Secretary }, Array.Empty<string>()),
        (Policies.AuditRead,            new[] { Chairman, Secretary, Auditor }, Array.Empty<string>()),
        (Policies.ReportExport,         new[] { Chairman, Secretary, Member, Reviewer, Auditor }, new[] { Submitter }),
        (Policies.AdminConfig,          new[] { Administrator }, Array.Empty<string>()),
    };

    public static IServiceCollection AddAcmpAuthorization(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RoleMappingOptions>(configuration.GetSection(RoleMappingOptions.SectionName));
        services.AddSingleton<IRoleClaimMapper, KeycloakRoleClaimMapper>();

        // ABAC handlers are scoped: they depend on the module-implemented resolvers (DbContext-backed).
        services.AddScoped<IAuthorizationHandler, CapabilityHandler>();
        services.AddScoped<IAuthorizationHandler, StreamScopeHandler>();

        services.AddAuthorization(options =>
        {
            foreach (var (policy, allow, owner) in Matrix)
                options.AddPolicy(policy, p => p.AddRequirements(new CapabilityRequirement(policy, allow, owner)));
        });

        return services;
    }

    // Exposed so the permission-matrix test can iterate the registered policy names without
    // re-declaring them; the expected A/AiO/D verdicts are encoded separately in the test.
    public static IReadOnlyCollection<string> RegisteredPolicies => Matrix.Select(m => m.Policy).ToArray();
}
