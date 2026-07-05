using Acmp.Modules.Risks.Domain.Enums;
using Acmp.Modules.Risks.Domain.Events;
using Acmp.Shared.Domain.Entities;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Risks.Domain;

// The Risk aggregate root (docs/domain/domain-model.md §Risk, docs/domain/entity-lifecycles.md §10; workflow W15) — a tracked architecture/delivery risk
// raised against a topic/decision/system/ADR, owning its Mitigations. Identity to other modules is by value
// only: (SubjectType, SubjectId = subject PublicId) + a display-key snapshot, never an EF navigation
// (ADR-0001). Severity/Exposure are DERIVED (RiskExposureScale), never stored (docs/domain/entity-lifecycles.md line 247).
//
// Lifecycle (W15): raise → Open; Open → Mitigating (needs ≥1 Mitigation); Mitigating → Closed (mitigations
// Done or a closure note); Open/Mitigating → Accepted (rationale + authority; terminal); Open/Mitigating →
// Escalated (reason + target). Escalated is transient — it returns to Mitigating (resume) or Closed after
// handling (docs/domain/entity-lifecycles.md §10 line 220). Closed and Accepted are terminal. The acceptance/escalation/closure
// evidence is stored on the aggregate (not audit-only) so the detail screen can show "why / to whom".
public sealed class Risk : AuditableEntity
{
    private readonly List<Mitigation> _mitigations = new();

    private Risk() { }

    // Optimistic-concurrency token (SQL rowversion). A stale write throws DbUpdateConcurrencyException → 409.
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public string Key { get; private set; } = string.Empty;          // RSK-YYYY-###
    public LocalizedString Title { get; private set; } = null!;
    public LocalizedString? Description { get; private set; }         // the design create form omits it → optional
    public RiskStatus Status { get; private set; }
    public RiskLevel Likelihood { get; private set; }
    public RiskLevel Impact { get; private set; }

    public string OwnerUserId { get; private set; } = string.Empty;  // Keycloak sub
    public string OwnerName { get; private set; } = string.Empty;    // display snapshot

    // Subject link (W15): the artifact the risk is raised against, plus a display-key snapshot for the
    // register's "Linked" column — no cross-module read (ADR-0001).
    public RiskSubjectType SubjectType { get; private set; }
    public Guid SubjectId { get; private set; }
    public string? SubjectKey { get; private set; }   // e.g. TOP-2026-014 — snapshot for the Linked column

    // Terminal/transition evidence (docs/domain/entity-lifecycles.md §10) — stored so the detail page can render it, not audit-only.
    public DateTimeOffset? ClosedAt { get; private set; }
    public LocalizedString? ClosureNote { get; private set; }
    public LocalizedString? AcceptanceRationale { get; private set; }
    public string? AcceptingAuthority { get; private set; }
    public LocalizedString? EscalationReason { get; private set; }
    public string? EscalationTarget { get; private set; }

    public IReadOnlyCollection<Mitigation> Mitigations => _mitigations.AsReadOnly();

    // Derived exposure overlay — the plain product of the two levels and its band (never persisted).
    public int Severity() => RiskExposureScale.Severity(Likelihood, Impact);
    public RiskExposure Exposure() => RiskExposureScale.Band(Likelihood, Impact);

    // W15: raise an Open risk against a subject. Owner + title + subject + likelihood/impact required.
    public static Risk Create(string key, LocalizedString title, LocalizedString? description,
        RiskLevel likelihood, RiskLevel impact, string ownerUserId, string ownerName,
        RiskSubjectType subjectType, Guid subjectId, string? subjectKey, DateTimeOffset now)
    {
        if (title is null) throw new InvalidOperationException("A risk title is required.");
        if (string.IsNullOrWhiteSpace(ownerUserId)) throw new InvalidOperationException("A risk owner is required.");
        if (subjectId == Guid.Empty) throw new InvalidOperationException("A risk must reference a subject artifact.");
        if (!Enum.IsDefined(likelihood)) throw new InvalidOperationException("A valid likelihood is required.");
        if (!Enum.IsDefined(impact)) throw new InvalidOperationException("A valid impact is required.");

        var risk = new Risk
        {
            Key = key.Trim(),
            Title = title,
            Description = description,
            Status = RiskStatus.Open,
            Likelihood = likelihood,
            Impact = impact,
            OwnerUserId = ownerUserId,
            OwnerName = (ownerName ?? string.Empty).Trim(),
            SubjectType = subjectType,
            SubjectId = subjectId,
            SubjectKey = subjectKey?.Trim(),
        };
        risk.Raise(new RiskRaisedEvent(risk.PublicId, risk.Key, ownerUserId, now));
        return risk;
    }

    // W15: plan a mitigation. Allowed while the risk is live (Open/Mitigating/Escalated), not once terminal.
    public Mitigation AddMitigation(LocalizedString description, MitigationType type,
        string? ownerUserId, Guid? linkedActionId, DateTimeOffset? dueDate)
    {
        RequireStatus(RiskStatus.Open, RiskStatus.Mitigating, RiskStatus.Escalated);
        if (!Enum.IsDefined(type)) throw new InvalidOperationException("A valid mitigation type is required.");
        var mitigation = Mitigation.Create(description, type, ownerUserId, linkedActionId, dueDate);
        _mitigations.Add(mitigation);
        return mitigation;
    }

    // W15: advance a mitigation's status (Planned → InProgress → Done). Allowed while the risk is live.
    public void SetMitigationStatus(Guid mitigationPublicId, MitigationStatus status)
    {
        RequireStatus(RiskStatus.Open, RiskStatus.Mitigating, RiskStatus.Escalated);
        var mitigation = _mitigations.FirstOrDefault(m => m.PublicId == mitigationPublicId)
            ?? throw new KeyNotFoundException("Mitigation not found on this risk.");
        mitigation.SetStatus(status);
    }

    // W15: begin mitigating. Open (or a returned-from-Escalated risk) → Mitigating; needs ≥1 Mitigation.
    public void BeginMitigation(DateTimeOffset now)
    {
        RequireStatus(RiskStatus.Open, RiskStatus.Escalated);
        if (_mitigations.Count == 0)
            throw new InvalidOperationException("At least one mitigation must be planned before mitigating.");
        Status = RiskStatus.Mitigating;
        Raise(new RiskMitigatingEvent(PublicId, Key, now));
    }

    // W15: close. Mitigating (or Escalated after handling) → Closed. Requires mitigations Done or a note.
    public void Close(LocalizedString? closureNote, DateTimeOffset now)
    {
        RequireStatus(RiskStatus.Mitigating, RiskStatus.Escalated);
        var mitigationsDone = _mitigations.Count > 0 && _mitigations.All(m => m.IsDone);
        if (!mitigationsDone && closureNote is null)
            throw new InvalidOperationException("A closure note is required unless all mitigations are done.");
        Status = RiskStatus.Closed;
        ClosureNote = closureNote;
        ClosedAt = now;
        Raise(new RiskClosedEvent(PublicId, Key, now));
    }

    // W15: accept the risk (consciously not mitigated). Open/Mitigating → Accepted (terminal). High-importance.
    public void Accept(LocalizedString rationale, string authority, DateTimeOffset now)
    {
        RequireStatus(RiskStatus.Open, RiskStatus.Mitigating);
        AcceptanceRationale = rationale ?? throw new InvalidOperationException("An acceptance rationale is required.");
        if (string.IsNullOrWhiteSpace(authority)) throw new InvalidOperationException("An accepting authority is required.");
        AcceptingAuthority = authority.Trim();
        Status = RiskStatus.Accepted;
        ClosedAt = now;
        Raise(new RiskAcceptedEvent(PublicId, Key, now));
    }

    // W15: escalate to a higher authority. Open/Mitigating → Escalated (transient). High-importance.
    public void Escalate(LocalizedString reason, string target, DateTimeOffset now)
    {
        RequireStatus(RiskStatus.Open, RiskStatus.Mitigating);
        EscalationReason = reason ?? throw new InvalidOperationException("An escalation reason is required.");
        if (string.IsNullOrWhiteSpace(target)) throw new InvalidOperationException("An escalation target is required.");
        EscalationTarget = target.Trim();
        Status = RiskStatus.Escalated;
        Raise(new RiskEscalatedEvent(PublicId, Key, EscalationTarget, now));
    }

    private void RequireStatus(params RiskStatus[] allowed)
    {
        if (Array.IndexOf(allowed, Status) < 0)
            throw new InvalidOperationException($"This operation is not allowed while the risk is {Status}.");
    }
}
