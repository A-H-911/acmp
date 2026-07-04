using Acmp.Modules.Governance.Domain.Enums;
using Acmp.Modules.Governance.Domain.Events;
using Acmp.Shared.Domain.Entities;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Governance.Domain;

// The ADR aggregate root (in-app ADR-YYYY-###; docs/11 §Governance, docs/12 §8; workflows W17/W21) — a
// MADR-lite architecture decision record owning its considered Options. Identity to other modules is by value
// only: SourceDecisionId = the promoting Decision's PublicId (W17), never an EF navigation (ADR-0001).
// Distinct from this package's planning ADR-#### files (README §F).
//
// Lifecycle (docs/12 §8): Draft (author/revise) → Proposed (submit for approval) → Approved (immutable, in
// force); Proposed → Draft on requested changes; Approved → Superseded (a successor ADR is approved) or
// Deprecated (retired without a replacement). The decisive rule (FR-101, ADR-0009): once Approved the
// content is FROZEN — there are no field setters, and a correction is a NEW ADR that supersedes this one.
public sealed class Adr : AuditableEntity
{
    private readonly List<AdrOption> _options = new();

    private Adr() { }

    // Optimistic-concurrency token (SQL rowversion). A stale write throws DbUpdateConcurrencyException → 409 (docs/16 §1.5, ADR-0018).
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public string Key { get; private set; } = string.Empty;   // ADR-YYYY-###
    public AdrStatus Status { get; private set; }
    public LocalizedString Title { get; private set; } = null!;

    // MADR-lite sections (docs/22 §A.7; FR-099). Context + Decision are required; drivers/consequences are
    // optional bilingual prose (markdown-as-text, the DV-04 model). Considered options are the owned list.
    public LocalizedString Context { get; private set; } = null!;
    public LocalizedString? DecisionDrivers { get; private set; }
    public LocalizedString DecisionText { get; private set; } = null!;
    public LocalizedString? ConsequencesPositive { get; private set; }
    public LocalizedString? ConsequencesNegative { get; private set; }

    // Author attribution snapshot (Keycloak sub + display name).
    public string AuthorUserId { get; private set; } = string.Empty;
    public string AuthorName { get; private set; } = string.Empty;

    // W17 promotion link (P11e): the committee Decision this ADR was promoted from (soft value ref, no FK).
    public Guid? SourceDecisionId { get; private set; }

    // Approval attribution (ADR-0009 — governance records are attributed by name).
    public DateTimeOffset? ApprovedAt { get; private set; }
    public string? ApprovedByUserId { get; private set; }
    public string? ApprovedByName { get; private set; }

    // Supersession chain (FR-101): both directions so the detail page renders "Supersedes" + "Superseded by".
    public Guid? SupersededByAdrId { get; private set; }
    public LocalizedString? SupersessionReason { get; private set; }
    public Guid? SupersedesAdrId { get; private set; }
    public LocalizedString? DeprecationReason { get; private set; }

    public IReadOnlyCollection<AdrOption> Options => _options.AsReadOnly();

    // W17: draft an ADR. Title + Context + Decision are required; a promoted ADR carries its source decision.
    public static Adr Draft(string key, LocalizedString title, LocalizedString context,
        LocalizedString? decisionDrivers, LocalizedString decisionText, LocalizedString? consequencesPositive,
        LocalizedString? consequencesNegative, IEnumerable<AdrOptionInput> options,
        string authorUserId, string authorName, Guid? sourceDecisionId, DateTimeOffset now)
    {
        if (title is null) throw new InvalidOperationException("An ADR title is required.");
        if (context is null) throw new InvalidOperationException("An ADR context is required.");
        if (decisionText is null) throw new InvalidOperationException("An ADR decision is required.");
        if (string.IsNullOrWhiteSpace(authorUserId)) throw new InvalidOperationException("An ADR author is required.");

        var adr = new Adr
        {
            Key = key.Trim(),
            Status = AdrStatus.Draft,
            Title = title,
            Context = context,
            DecisionDrivers = decisionDrivers,
            DecisionText = decisionText,
            ConsequencesPositive = consequencesPositive,
            ConsequencesNegative = consequencesNegative,
            AuthorUserId = authorUserId,
            AuthorName = (authorName ?? string.Empty).Trim(),
            SourceDecisionId = sourceDecisionId,
        };
        adr.ReplaceOptions(options);
        adr.Raise(new AdrDraftedEvent(adr.PublicId, adr.Key, authorUserId, now));
        return adr;
    }

    // Revise a Draft (the request-changes loop returns Proposed → Draft, then the author edits here). Allowed
    // ONLY while Draft — an Approved ADR is immutable (FR-101).
    public void UpdateDraft(LocalizedString title, LocalizedString context, LocalizedString? decisionDrivers,
        LocalizedString decisionText, LocalizedString? consequencesPositive, LocalizedString? consequencesNegative,
        IEnumerable<AdrOptionInput> options)
    {
        RequireStatus(AdrStatus.Draft);
        Title = title ?? throw new InvalidOperationException("An ADR title is required.");
        Context = context ?? throw new InvalidOperationException("An ADR context is required.");
        DecisionText = decisionText ?? throw new InvalidOperationException("An ADR decision is required.");
        DecisionDrivers = decisionDrivers;
        ConsequencesPositive = consequencesPositive;
        ConsequencesNegative = consequencesNegative;
        ReplaceOptions(options);
    }

    // W17: submit for approval. Draft → Proposed. Required MADR sections are already enforced at draft time.
    public void Propose(DateTimeOffset now)
    {
        RequireStatus(AdrStatus.Draft);
        Status = AdrStatus.Proposed;
        Raise(new AdrProposedEvent(PublicId, Key, now));
    }

    // Reviewer/Secretary requests changes: Proposed → Draft (the author revises via UpdateDraft).
    public void RequestChanges(DateTimeOffset now)
    {
        RequireStatus(AdrStatus.Proposed);
        Status = AdrStatus.Draft;
        Raise(new AdrChangesRequestedEvent(PublicId, Key, now));
    }

    // W17: approve. Proposed → Approved; attribution recorded; immutable thereafter (FR-100/101, high audit).
    public void Approve(string approverUserId, string approverName, DateTimeOffset now)
    {
        RequireStatus(AdrStatus.Proposed);
        if (string.IsNullOrWhiteSpace(approverUserId)) throw new InvalidOperationException("An approver is required.");
        Status = AdrStatus.Approved;
        ApprovedAt = now;
        ApprovedByUserId = approverUserId;
        ApprovedByName = (approverName ?? string.Empty).Trim();
        Raise(new AdrApprovedEvent(PublicId, Key, now));
    }

    // W21 (ADR half): supersede an approved ADR with its replacement. Approved → Superseded; the back-link +
    // reason are recorded immutably; the prior ADR's content is left untouched (FR-101).
    public void Supersede(Guid supersededByAdrId, LocalizedString reason, DateTimeOffset now)
    {
        RequireStatus(AdrStatus.Approved);
        if (supersededByAdrId == Guid.Empty) throw new InvalidOperationException("A superseding ADR is required.");
        if (reason is null) throw new InvalidOperationException("A supersession reason is required.");
        Status = AdrStatus.Superseded;
        SupersededByAdrId = supersededByAdrId;
        SupersessionReason = reason;
        Raise(new AdrSupersededEvent(PublicId, Key, supersededByAdrId, now));
    }

    // Record the forward link on the successor (it supersedes the prior ADR). Set once, at supersession time.
    public void MarkSupersedes(Guid priorAdrId)
    {
        if (priorAdrId == Guid.Empty) throw new InvalidOperationException("A prior ADR is required.");
        SupersedesAdrId = priorAdrId;
    }

    // W21: deprecate an approved ADR without a replacement. Approved → Deprecated (rationale recorded).
    public void Deprecate(LocalizedString reason, DateTimeOffset now)
    {
        RequireStatus(AdrStatus.Approved);
        DeprecationReason = reason ?? throw new InvalidOperationException("A deprecation rationale is required.");
        Status = AdrStatus.Deprecated;
        Raise(new AdrDeprecatedEvent(PublicId, Key, now));
    }

    private void ReplaceOptions(IEnumerable<AdrOptionInput> options)
    {
        _options.Clear();
        foreach (var o in options ?? Enumerable.Empty<AdrOptionInput>())
            _options.Add(AdrOption.Create(o.Name, o.Body, o.IsChosen));
    }

    private void RequireStatus(params AdrStatus[] allowed)
    {
        if (Array.IndexOf(allowed, Status) < 0)
            throw new InvalidOperationException($"This operation is not allowed while the ADR is {Status}.");
    }
}
