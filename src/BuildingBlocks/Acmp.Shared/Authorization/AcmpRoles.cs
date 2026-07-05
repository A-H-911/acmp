namespace Acmp.Shared.Authorization;

// Canonical global role names (README §C / docs/domain/permission-role-matrix.md §B). These string codes are the single
// authorization vocabulary: token role claims map TO them (IRoleClaimMapper), ASP.NET policies
// require them, and Membership's CommitteeRole enum members are named to match (nameof aligns).
// Guest/Presenter is the single global role "Guest"; the Presenter ability is the per-topic
// relationship (docs/domain/permission-role-matrix.md §D), not a separate role.
public static class AcmpRoles
{
    public const string Chairman = "Chairman";
    public const string Secretary = "Secretary";
    public const string Member = "Member";
    public const string Reviewer = "Reviewer";
    public const string Auditor = "Auditor";
    public const string Administrator = "Administrator";
    public const string Submitter = "Submitter";
    public const string Guest = "Guest";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Chairman, Secretary, Member, Reviewer, Auditor, Administrator, Submitter, Guest,
    };
}
