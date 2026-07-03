namespace Acmp.Shared.Authorization;

// Named authorization policy constants — one per row of the docs/10 §C capability matrix.
// Policies are registered centrally (AuthorizationRegistration) and enforced at the owning
// module's endpoints; many target aggregates ship in later phases, but the contract is fixed in P4.
public static class Policies
{
    public const string TopicSubmit = "Topic.Submit";
    public const string TopicTriage = "Topic.Triage";
    public const string TopicEdit = "Topic.Edit";
    public const string BacklogPrioritize = "Backlog.Prioritize";
    public const string AgendaPublish = "Agenda.Publish";
    public const string MeetingSchedule = "Meeting.Schedule";
    public const string AttendanceRecord = "Attendance.Record";
    public const string MinutesCapture = "Minutes.Capture";
    public const string MinutesApprove = "Minutes.Approve";
    public const string VoteManage = "Vote.Manage";
    public const string VoteCast = "Vote.Cast";
    public const string DecisionRecord = "Decision.Record";
    public const string DecisionChairApprove = "Decision.ChairApprove";
    public const string ActionCreate = "Action.Create";
    public const string ActionVerify = "Action.Verify";
    public const string RiskManage = "Risk.Manage";
    public const string RiskAccept = "Risk.Accept";
    public const string DependencyCreate = "Dependency.Create";
    public const string TraceabilityLink = "Traceability.Link";
    public const string AdrCreate = "Adr.Create";
    public const string AdrApprove = "Adr.Approve";
    public const string AdrSupersede = "Adr.Supersede";
    public const string InvariantCreate = "Invariant.Create";
    public const string InvariantApprove = "Invariant.Approve";
    public const string TemplateManage = "Template.Manage";
    public const string DocumentManage = "Document.Manage";
    public const string DiagramAttach = "Diagram.Attach";
    public const string ResearchManage = "Research.Manage";
    public const string AdminUsers = "Admin.Users";
    public const string AuthDelegate = "Auth.Delegate";
    public const string AuditRead = "Audit.Read";
    public const string ReportExport = "Report.Export";
    public const string AdminConfig = "Admin.Config";
}
