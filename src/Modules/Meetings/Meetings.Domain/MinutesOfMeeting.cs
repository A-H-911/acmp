using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Modules.Meetings.Domain.Events;
using Acmp.Shared.Domain.Entities;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Meetings.Domain;

// The MinutesOfMeeting aggregate root (docs/11 §Meetings, docs/12 §6, W10) — the versioned official
// record of a meeting. Homed IN the Meetings module (docs/11 §B lists it here), so it references its
// Meeting by id + display snapshots (MeetingKey/MeetingTitle), never a cross-module read (ADR-0001).
//
// Immutability (AC-036, ADR-0009): once Published, NOTHING is editable — a correction is a NEW version
// (same Key, Version++) that supersedes the prior; the prior stays readable and is never edited. Content
// is editable only while Draft (Revise). Approve/Publish are two distinct transitions (operator-locked
// 5-state): Approve records the approver + the soft-SoD-2 sole-author flag (AC-014); Publish seals the
// record and drives the notify-all fan-out (AC-038).
//
// ponytail: the documented `Content:json (structured sections)` (docs/11) is modelled as a single
// bilingual markdown `Summary` (LocalizedString, mirrored EN===AR) — structured sections aren't needed by
// any P7c AC (AC-014/036/037/038) and align with the one-editor markdown-as-text decision (DV-04/AM-06).
// Flagged as a data-model deviation, not a silent drift (guardrail 11).
public sealed class MinutesOfMeeting : AuditableEntity
{
    private MinutesOfMeeting() { }

    // Optimistic-concurrency token (SQL rowversion). A stale write throws DbUpdateConcurrencyException → API 409 (docs/16 §1.5, ADR-0018).
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public string Key { get; private set; } = string.Empty;      // MIN-YYYY-### — stable across versions of one meeting's minutes
    public int Version { get; private set; }                       // 1-based; a supersession creates Version+1 under the SAME Key
    public Guid MeetingId { get; private set; }                    // Meeting.PublicId
    public string MeetingKey { get; private set; } = string.Empty; // MTG-YYYY-### display snapshot (deep-link target)
    public string MeetingTitle { get; private set; } = string.Empty; // display snapshot (notification/header)
    public MinutesStatus Status { get; private set; }
    public LocalizedString Summary { get; private set; } = null!;  // bilingual markdown body (mirrored EN===AR)

    // Approval attribution recorded at approve time (SoD-2 soft: sole-author approvals are allowed but flagged, AC-014).
    public string? ApprovedByUserId { get; private set; }          // Keycloak subject
    public string? ApprovedByName { get; private set; }            // display snapshot
    public DateTimeOffset? ApprovedAt { get; private set; }
    public bool ApprovedBySoleAuthor { get; private set; }         // AC-014 warning indicator
    public DateTimeOffset? PublishedAt { get; private set; }

    // Supersession back-link (AC-036): the version that replaced this one + why.
    public Guid? SupersededByMinutesId { get; private set; }
    public LocalizedString? SupersessionReason { get; private set; }

    // W10: start the MoM as an editable Draft (the caller guards the parent meeting is InProgress/Held).
    public static MinutesOfMeeting Draft(string key, Guid meetingId, string meetingKey, string meetingTitle,
        LocalizedString summary, DateTimeOffset now)
    {
        var minutes = NewVersion(key, meetingId, meetingKey, meetingTitle, summary, version: 1, MinutesStatus.Draft);
        minutes.Raise(new MinutesDraftedEvent(minutes.PublicId, minutes.Key, meetingId, minutes.Version, now));
        return minutes;
    }

    // W10: edit the draft body (autosave). Draft-only — Approved/Published are immutable (AC-036).
    public void Revise(LocalizedString summary, DateTimeOffset now)
    {
        RequireStatus(MinutesStatus.Draft);
        Summary = summary ?? throw new InvalidOperationException("A summary is required.");
    }

    // W10: submit for review. Draft → InReview.
    public void SubmitForReview(DateTimeOffset now)
    {
        RequireStatus(MinutesStatus.Draft);
        Status = MinutesStatus.InReview;
        Raise(new MinutesInReviewEvent(PublicId, Key, now));
    }

    // W10 (AC-037): the reviewer requests changes. InReview → Draft; the author is re-notified by the handler.
    public void RequestChanges(DateTimeOffset now)
    {
        RequireStatus(MinutesStatus.InReview);
        Status = MinutesStatus.Draft;
        Raise(new MinutesChangesRequestedEvent(PublicId, Key, now));
    }

    // W10: approve the MoM (SoD-2 is SOFT — a sole-author approval is ALLOWED but flagged, AC-014).
    // InReview → Approved. Approval does NOT publish or notify (that is a separate transition).
    public void Approve(string approverSub, string approverName, bool isSoleAuthor, DateTimeOffset now)
    {
        RequireStatus(MinutesStatus.InReview);
        Status = MinutesStatus.Approved;
        ApprovedByUserId = approverSub;
        ApprovedByName = approverName.Trim();
        ApprovedAt = now;
        ApprovedBySoleAuthor = isSoleAuthor;
        Raise(new MinutesApprovedEvent(PublicId, Key, isSoleAuthor, now));
    }

    // W10 (AC-038): publish the approved MoM. Approved → Published; immutable thereafter. The handler fans
    // out the in-app notification to every active member on this transition.
    public void Publish(DateTimeOffset now)
    {
        RequireStatus(MinutesStatus.Approved);
        Status = MinutesStatus.Published;
        PublishedAt = now;
        Raise(new MinutesPublishedEvent(PublicId, Key, Version, now));
    }

    // W10 (AC-036): supersede this version with its replacement. Approved/Published → Superseded; the
    // back-link + reason are recorded immutably; this version's content is left untouched (never edited).
    public void Supersede(Guid supersededByMinutesId, LocalizedString reason, DateTimeOffset now)
    {
        RequireStatus(MinutesStatus.Approved, MinutesStatus.Published);
        if (supersededByMinutesId == Guid.Empty)
            throw new InvalidOperationException("A superseding minutes version is required.");
        Status = MinutesStatus.Superseded;
        SupersededByMinutesId = supersededByMinutesId;
        SupersessionReason = reason ?? throw new InvalidOperationException("A supersession reason is required.");
        Raise(new MinutesSupersededEvent(PublicId, Key, supersededByMinutesId, now));
    }

    // W10 (AC-036): the corrected successor version. Built already Published in one transaction (the actor
    // holds Minutes.Approve and is correcting an already-approved record — consistent with the decision
    // supersede-creates-issued-successor pattern, a blessed deviation). Same Key, Version+1.
    public static MinutesOfMeeting PublishedCorrection(string key, Guid meetingId, string meetingKey, string meetingTitle,
        int version, LocalizedString summary, string actorSub, string actorName, DateTimeOffset now)
    {
        var minutes = NewVersion(key, meetingId, meetingKey, meetingTitle, summary, version, MinutesStatus.Published);
        minutes.ApprovedByUserId = actorSub;
        minutes.ApprovedByName = actorName.Trim();
        minutes.ApprovedAt = now;
        minutes.ApprovedBySoleAuthor = false; // a supersession, not the normal sole-author approval scenario (AC-014)
        minutes.PublishedAt = now;
        minutes.Raise(new MinutesPublishedEvent(minutes.PublicId, minutes.Key, minutes.Version, now));
        return minutes;
    }

    private static MinutesOfMeeting NewVersion(string key, Guid meetingId, string meetingKey, string meetingTitle,
        LocalizedString summary, int version, MinutesStatus status)
    {
        if (meetingId == Guid.Empty) throw new InvalidOperationException("Minutes must reference a meeting.");
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("A minutes key is required.");
        if (summary is null) throw new InvalidOperationException("A summary is required.");

        return new MinutesOfMeeting
        {
            Key = key.Trim(),
            Version = version,
            MeetingId = meetingId,
            MeetingKey = (meetingKey ?? string.Empty).Trim(),
            MeetingTitle = (meetingTitle ?? string.Empty).Trim(),
            Summary = summary,
            Status = status,
        };
    }

    private void RequireStatus(params MinutesStatus[] allowed)
    {
        if (Array.IndexOf(allowed, Status) < 0)
            throw new InvalidOperationException($"This operation is not allowed while the minutes are {Status}.");
    }
}
