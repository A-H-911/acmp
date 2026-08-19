using Acmp.Shared.Authorization.Abac;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static Acmp.Shared.Authorization.AcmpRoles;

namespace Acmp.Shared.Authorization;

// Composition root for ACMP authorization: the Keycloak role-claim mapper, the ABAC handlers, and
// the named-policy registry encoding the docs/domain/permission-role-matrix.md §C capability matrix. Each policy is a single
// CapabilityRequirement (full-allow roles + allow-if-owner roles); Deny is the absence of both, so
// Administrator's exclusion from committee content (SoD-5) is structural — it is simply never
// listed on a content row.
public static class AuthorizationRegistration
{
    // Row = (policy, full-Allow roles, Allow-if-owner roles). Transcribed from docs/domain/permission-role-matrix.md §C.
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
        (Policies.AdrPromote,           new[] { Chairman }, Array.Empty<string>()),
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

    /// <summary>
    /// The policies that additionally carry <see cref="StreamScopeRequirement"/> — ADR-0043 step 7,
    /// closing DEF-057, where the handler was registered, unit-tested, and in no policy at all.
    /// </summary>
    /// <remarks>
    /// ⚠ ONE ENTRY, AND THE SHORTNESS IS THE FINDING RATHER THAN AN OVERSIGHT. permission-role-matrix
    /// §E.1 bounds a Member/Reviewer/Submitter to "topics intersecting their assigned streams", and
    /// Policies.TopicEdit is the only policy that is BOTH stream-bounded and evaluated with a
    /// resource: TopicTriage and BacklogPrioritize allow Chairman/Secretary only, who bypass;
    /// TopicSubmit is endpoint-level with no resource; DiagramAttach has no EnsureAsync call site.
    /// <para>
    /// ⚠ ADDING A POLICY HERE IS NOT A FREE CHOICE — READ DEF-068 FIRST. StreamScopeHandler is
    /// AuthorizationHandler&lt;TRequirement, TResource&gt;, which ASP.NET NEVER invokes when the
    /// resource is absent or is not an IStreamScopedResource. The requirement then goes unsatisfied
    /// and the policy refuses EVERYONE, the Chairman included. That is fail-closed, which is
    /// ADR-0043's stated posture and is the safe direction — but it is a 403 no message explains, so
    /// a policy belongs here only when EVERY call site passes a stream-scoped aggregate. Today that
    /// means: never used with `.RequireAuthorization(...)` at endpoint level, and every
    /// IResourceAuthorizer.EnsureAsync call passes Topic.
    /// </para>
    /// </remarks>
    public static readonly IReadOnlySet<string> StreamScoped = new HashSet<string> { Policies.TopicEdit };

    /// <summary>
    /// Policies that additionally carry <see cref="ConfidentialityRequirement"/> (C-AUTHZ-04 / FR-163).
    /// </summary>
    /// <remarks>
    /// ⚠ THE SAME DEF-068 RULE APPLIES, AND IT EXCLUDES THE POLICY YOU WOULD REACH FOR FIRST.
    /// ConfidentialityHandler is a two-parameter handler, so ASP.NET never invokes it without an
    /// IConfidentialResource, and the policy would then refuse EVERYONE including the Chairman.
    /// <para>
    /// <c>Policies.TopicEdit</c> qualifies: its group is bare and every call site is
    /// <c>IResourceAuthorizer.EnsureAsync(topic, ...)</c> inside UpdateTopicHandler.
    /// <c>Policies.TopicTriage</c> DOES NOT, and must never be added: it is applied with
    /// <c>.RequireAuthorization(Policies.TopicTriage)</c> at endpoint level on /close, /reopen,
    /// /reactivate and /convert, where there is no resource at all.
    /// </para>
    /// <para>
    /// The read side is NOT covered here and cannot be — no topic read path calls IAuthorizationService.
    /// Exclusion from lists, detail and search is a query predicate; see ConfidentialityRequirement's
    /// header. Two mechanisms, one control.
    /// </para>
    /// </remarks>
    public static readonly IReadOnlySet<string> ConfidentialityScoped = new HashSet<string> { Policies.TopicEdit };

    public static IServiceCollection AddAcmpAuthorization(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RoleMappingOptions>(configuration.GetSection(RoleMappingOptions.SectionName));
        services.AddSingleton<IRoleClaimMapper, KeycloakRoleClaimMapper>();

        // ABAC handlers are scoped: they depend on the module-implemented resolvers (DbContext-backed).
        services.AddScoped<IAuthorizationHandler, CapabilityHandler>();
        services.AddScoped<IAuthorizationHandler, StreamScopeHandler>();
        services.AddScoped<IAuthorizationHandler, ConfidentialityHandler>();

        services.AddAuthorization(options =>
        {
            foreach (var (policy, allow, owner) in Matrix)
                options.AddPolicy(policy, p =>
                {
                    p.AddRequirements(new CapabilityRequirement(policy, allow, owner));
                    if (StreamScoped.Contains(policy))
                        p.AddRequirements(new StreamScopeRequirement());
                    if (ConfidentialityScoped.Contains(policy))
                        p.AddRequirements(new ConfidentialityRequirement());
                });
        });

        return services;
    }

    // Exposed so the permission-matrix test can iterate the registered policy names without
    // re-declaring them; the expected A/AiO/D verdicts are encoded separately in the test.
    public static IReadOnlyCollection<string> RegisteredPolicies => Matrix.Select(m => m.Policy).ToArray();
}
