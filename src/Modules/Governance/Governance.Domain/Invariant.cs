using Acmp.Modules.Governance.Domain.Enums;
using Acmp.Modules.Governance.Domain.Events;
using Acmp.Shared.Domain.Entities;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Governance.Domain;

// The Architecture Invariant aggregate root (in-app AIV-YYYY-###; docs/11 §Governance, docs/12 §9, docs/22
// §A.5; workflow W18) — a standing structural rule the governed estate must always exhibit. Sibling of the
// Adr aggregate in the same module.
//
// Concept fidelity (docs/22 §A, the concept single-source-of-truth per README §G): an Architecture Invariant
// is DISTINCT from Principle/Standard/Policy/Constraint — those are their own concepts (Policy is an external
// register ACMP only consumes; Constraint is a CON-### planning id). So there is deliberately NO "Kind"
// enum folding them in — docs/12 §9 mentions a "kind" that FR-106 and the design create-form both omit; §A
// governs the tie (OQ-P11c-1, dropped by operator 2026-07-04). The invariant carries Category + Scope only.
//
// Lifecycle (docs/12 §9, FR-107): Draft (author/revise) → Proposed (submit for approval) → Active (in force,
// immutable); Proposed → Draft on requested changes; Active → Retired (rationale) or Superseded (a successor
// invariant is activated). Once Active the statement is FROZEN — a correction is a NEW invariant that
// supersedes this one (ADR-0009, supersede-not-edit). Violations are tracked separately (Risk/Action/Audit),
// never as a state here (docs/22 §A.5). docs/12 §9 splits its Draft/Proposed field guards, but the design's
// single create-dialog collects every required field at once, so we require them all at Draft (as Adr does).
public sealed class Invariant : AuditableEntity
{
    private Invariant() { }

    // Optimistic-concurrency token (SQL rowversion). A stale write throws DbUpdateConcurrencyException → 409 (docs/16 §1.5, ADR-0018).
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public string Key { get; private set; } = string.Empty;   // AIV-YYYY-###
    public InvariantStatus Status { get; private set; }
    public InvariantCategory Category { get; private set; }
    public InvariantScope Scope { get; private set; }

    // The rule itself (FR-106). Statement + Rationale are required bilingual prose; ExceptionsPolicy is the
    // optional "how exceptions are handled" note (docs/22 §A.5). Markdown-as-text (the DV-04 model).
    public LocalizedString Statement { get; private set; } = null!;
    public LocalizedString Rationale { get; private set; } = null!;
    public LocalizedString? ExceptionsPolicy { get; private set; }

    // The designated owner (FR-106 "owner (required)") — a Keycloak subject + display-name snapshot, not an FK.
    public string OwnerUserId { get; private set; } = string.Empty;
    public string OwnerName { get; private set; } = string.Empty;

    // Activation attribution (ADR-0009 — governance records are attributed by name).
    public DateTimeOffset? ActivatedAt { get; private set; }
    public string? ActivatedByUserId { get; private set; }
    public string? ActivatedByName { get; private set; }

    // Supersession chain (FR-107): both directions so the detail page renders "Supersedes" + "Superseded by".
    public Guid? SupersededByInvariantId { get; private set; }
    public LocalizedString? SupersessionReason { get; private set; }
    public Guid? SupersedesInvariantId { get; private set; }
    public LocalizedString? RetirementReason { get; private set; }

    // W18: draft an invariant. Category/Scope/Statement/Rationale/Owner are all required (single-step create).
    public static Invariant Draft(string key, InvariantCategory category, InvariantScope scope,
        LocalizedString statement, LocalizedString rationale, LocalizedString? exceptionsPolicy,
        string ownerUserId, string ownerName, DateTimeOffset now)
    {
        if (statement is null) throw new InvalidOperationException("An invariant statement is required.");
        if (rationale is null) throw new InvalidOperationException("An invariant rationale is required.");
        if (string.IsNullOrWhiteSpace(ownerUserId)) throw new InvalidOperationException("An invariant owner is required.");

        var inv = new Invariant
        {
            Key = key.Trim(),
            Status = InvariantStatus.Draft,
            Category = category,
            Scope = scope,
            Statement = statement,
            Rationale = rationale,
            ExceptionsPolicy = exceptionsPolicy,
            OwnerUserId = ownerUserId,
            OwnerName = (ownerName ?? string.Empty).Trim(),
        };
        inv.Raise(new InvariantDraftedEvent(inv.PublicId, inv.Key, ownerUserId, now));
        return inv;
    }

    // Revise a Draft (the request-changes loop returns Proposed → Draft, then the owner edits here). Allowed
    // ONLY while Draft — an Active invariant is immutable.
    public void UpdateDraft(InvariantCategory category, InvariantScope scope, LocalizedString statement,
        LocalizedString rationale, LocalizedString? exceptionsPolicy, string ownerUserId, string ownerName)
    {
        RequireStatus(InvariantStatus.Draft);
        Statement = statement ?? throw new InvalidOperationException("An invariant statement is required.");
        Rationale = rationale ?? throw new InvalidOperationException("An invariant rationale is required.");
        if (string.IsNullOrWhiteSpace(ownerUserId)) throw new InvalidOperationException("An invariant owner is required.");
        Category = category;
        Scope = scope;
        ExceptionsPolicy = exceptionsPolicy;
        OwnerUserId = ownerUserId;
        OwnerName = (ownerName ?? string.Empty).Trim();
    }

    // W18: submit for approval. Draft → Proposed.
    public void Propose(DateTimeOffset now)
    {
        RequireStatus(InvariantStatus.Draft);
        Status = InvariantStatus.Proposed;
        Raise(new InvariantProposedEvent(PublicId, Key, now));
    }

    // Reviewer/Secretary requests changes: Proposed → Draft (the owner revises via UpdateDraft).
    public void RequestChanges(DateTimeOffset now)
    {
        RequireStatus(InvariantStatus.Proposed);
        Status = InvariantStatus.Draft;
        Raise(new InvariantChangesRequestedEvent(PublicId, Key, now));
    }

    // W18: approve/activate. Proposed → Active; attribution recorded; immutable thereafter (high audit).
    public void Activate(string approverUserId, string approverName, DateTimeOffset now)
    {
        RequireStatus(InvariantStatus.Proposed);
        if (string.IsNullOrWhiteSpace(approverUserId)) throw new InvalidOperationException("An approver is required.");
        Status = InvariantStatus.Active;
        ActivatedAt = now;
        ActivatedByUserId = approverUserId;
        ActivatedByName = (approverName ?? string.Empty).Trim();
        Raise(new InvariantActivatedEvent(PublicId, Key, now));
    }

    // W21 (invariant half): supersede an active invariant with its replacement. Active → Superseded; the
    // back-link + reason are recorded immutably; the prior statement is left untouched.
    public void Supersede(Guid supersededByInvariantId, LocalizedString reason, DateTimeOffset now)
    {
        RequireStatus(InvariantStatus.Active);
        if (supersededByInvariantId == Guid.Empty) throw new InvalidOperationException("A superseding invariant is required.");
        if (reason is null) throw new InvalidOperationException("A supersession reason is required.");
        Status = InvariantStatus.Superseded;
        SupersededByInvariantId = supersededByInvariantId;
        SupersessionReason = reason;
        Raise(new InvariantSupersededEvent(PublicId, Key, supersededByInvariantId, now));
    }

    // Record the forward link on the successor (it supersedes the prior invariant). Set once, at supersession time.
    public void MarkSupersedes(Guid priorInvariantId)
    {
        if (priorInvariantId == Guid.Empty) throw new InvalidOperationException("A prior invariant is required.");
        SupersedesInvariantId = priorInvariantId;
    }

    // W21: retire an active invariant without a replacement. Active → Retired (rationale recorded).
    public void Retire(LocalizedString reason, DateTimeOffset now)
    {
        RequireStatus(InvariantStatus.Active);
        RetirementReason = reason ?? throw new InvalidOperationException("A retirement rationale is required.");
        Status = InvariantStatus.Retired;
        Raise(new InvariantRetiredEvent(PublicId, Key, now));
    }

    private void RequireStatus(params InvariantStatus[] allowed)
    {
        if (Array.IndexOf(allowed, Status) < 0)
            throw new InvalidOperationException($"This operation is not allowed while the invariant is {Status}.");
    }
}
