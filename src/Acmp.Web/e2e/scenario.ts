import { type APIRequestContext, type Locator } from '@playwright/test';

/*
 * S6b-2 (ADR-0016 §2) setup helpers. The drag paths and failure cases are the behaviour under
 * test; getting a topic Prepared or a meeting+agenda into place is incidental, so we drive that
 * setup through the API (with a real captured bearer) and reserve the UI for the action being
 * asserted. Keeps each spec fast and non-flaky.
 */

const JSON_HEADERS = { 'Content-Type': 'application/json' } as const;

export interface ApiMember {
  publicId: string;
  /** The Keycloak sub — the identity votes/ballots are keyed by (NOT publicId). */
  keycloakUserId: string;
  fullName: string;
  role: string;
  isActive: boolean;
}
export interface ApiTopic {
  id: string;
  key: string;
  title: string;
}

export async function apiMembers(request: APIRequestContext, bearer: string): Promise<ApiMember[]> {
  const res = await request.get('/api/members', { headers: { Authorization: bearer } });
  if (!res.ok()) throw new Error(`[e2e] GET members ${res.status()}`);
  return res.json();
}

/** Create a topic via the API (stays in Triage); returns its id/key/title. */
export async function apiCreateTopic(request: APIRequestContext, bearer: string, title: string): Promise<ApiTopic> {
  const create = await request.post('/api/topics', {
    headers: { Authorization: bearer, ...JSON_HEADERS },
    data: {
      type: 'ArchitectureDecision',
      title,
      description: 'E2E setup topic.',
      justification: 'E2E setup justification.',
      streams: ['Platform'],
      systems: [],
      urgency: 'Normal',
      source: 'CommitteeMember',
      tags: [],
    },
  });
  if (create.status() !== 201) throw new Error(`[e2e] create topic ${create.status()} ${await create.text()}`);
  const topic = (await create.json()) as { id: string; key: string };
  return { ...topic, title };
}

/** Create → accept (owner) → prepare a topic via the API, returning its id/key/title. */
export async function apiPreparedTopic(
  request: APIRequestContext,
  bearer: string,
  title: string,
  owner: ApiMember,
): Promise<ApiTopic> {
  const topic = await apiCreateTopic(request, bearer, title);

  const accept = await request.post(`/api/topics/${topic.id}/accept`, {
    headers: { Authorization: bearer, ...JSON_HEADERS },
    data: { ownerId: owner.publicId, ownerName: owner.fullName },
  });
  if (!accept.ok()) throw new Error(`[e2e] accept ${accept.status()} ${await accept.text()}`);

  const prepare = await request.post(`/api/topics/${topic.id}/prepare`, { headers: { Authorization: bearer } });
  if (!prepare.ok()) throw new Error(`[e2e] prepare ${prepare.status()} ${await prepare.text()}`);

  return { ...topic, title };
}

export interface ApiMeeting {
  id: string;
  key: string;
}

/** Schedule a meeting via the API (single-day window); returns its id/key. */
export async function apiScheduleMeeting(
  request: APIRequestContext,
  bearer: string,
  title: string,
  chair: ApiMember,
): Promise<ApiMeeting> {
  const res = await request.post('/api/meetings', {
    headers: { Authorization: bearer, ...JSON_HEADERS },
    data: {
      title,
      chairUserId: chair.publicId,
      chairName: chair.fullName,
      scheduledStart: '2026-09-01T14:00:00.000Z',
      scheduledEnd: '2026-09-01T15:00:00.000Z',
      type: 'Regular',
      mode: 'InPerson',
    },
  });
  if (res.status() !== 201) throw new Error(`[e2e] schedule meeting ${res.status()} ${await res.text()}`);
  return res.json();
}

/** Add a prepared topic to a meeting's agenda via the API (presenter required to later publish). */
export async function apiAddAgendaItem(
  request: APIRequestContext,
  bearer: string,
  meetingId: string,
  topic: ApiTopic,
  presenter: ApiMember,
): Promise<void> {
  const res = await request.post(`/api/meetings/${meetingId}/agenda/items`, {
    headers: { Authorization: bearer, ...JSON_HEADERS },
    data: {
      topicId: topic.id,
      topicKey: topic.key,
      topicTitle: topic.title,
      urgent: false,
      timeboxMinutes: 15,
      presenterUserId: presenter.publicId,
      presenterName: presenter.fullName,
    },
  });
  if (!res.ok()) throw new Error(`[e2e] add agenda item ${res.status()} ${await res.text()}`);
}

export interface ApiAction {
  id: string;
  key: string;
  status: string;
}

/**
 * Create a follow-up action (P17b actions cluster). Owner = the given member's KC sub (`keycloakUserId`,
 * the identity SoD-1 is keyed by). The source is a create-time snapshot (no cross-module FK — SourceId is
 * only NotEmpty-validated, ADR-0001), so any seeded artifact id works; the default anchors it to a Topic.
 */
export async function apiCreateAction(
  request: APIRequestContext,
  bearer: string,
  opts: { title: string; ownerUserId: string; ownerName: string; sourceId: string; sourceType?: string; priority?: string; dueDate?: string },
): Promise<ApiAction> {
  const res = await request.post('/api/actions', {
    headers: { Authorization: bearer, ...JSON_HEADERS },
    data: {
      title: { en: opts.title, ar: opts.title },
      description: null,
      priority: opts.priority ?? 'Normal',
      ownerUserId: opts.ownerUserId,
      ownerName: opts.ownerName,
      dueDate: opts.dueDate ?? null,
      sourceType: opts.sourceType ?? 'Topic',
      sourceId: opts.sourceId,
      sourceKey: null,
      meetingKey: null,
    },
  });
  if (res.status() !== 201) throw new Error(`[e2e] create action ${res.status()} ${await res.text()}`);
  return res.json();
}

/** Start an action (Open → InProgress). Attributed to the caller. */
export async function apiStartAction(request: APIRequestContext, bearer: string, actionId: string): Promise<void> {
  const res = await request.post(`/api/actions/${actionId}/start`, { headers: { Authorization: bearer } });
  if (!res.ok()) throw new Error(`[e2e] start action ${res.status()} ${await res.text()}`);
}

/** Complete an action (→ Completed). CompletedByUserId = the caller's sub — the SoD-1 completer. */
export async function apiCompleteAction(request: APIRequestContext, bearer: string, actionId: string): Promise<void> {
  const res = await request.post(`/api/actions/${actionId}/complete`, {
    headers: { Authorization: bearer, ...JSON_HEADERS },
    data: { completionNote: null },
  });
  if (!res.ok()) throw new Error(`[e2e] complete action ${res.status()} ${await res.text()}`);
}

export interface ApiDecision {
  id: string;
  key: string;
  status: string;
}

/**
 * Record a decision (P17b decisions cluster) → Draft. The default outcome `Deferred` is a NON-follow-up
 * outcome, so it issues freely (no AC-029 downstream-link gate); with no VoteId the SoD-3 / vote-coupling
 * gates are skipped too — the cheapest path to a seed Issued decision. Policy DecisionRecord (Sec/Chair).
 */
export async function apiRecordDecision(
  request: APIRequestContext,
  bearer: string,
  opts: { topicId: string; title: string; statement: string; rationale: string; outcome?: string },
): Promise<ApiDecision> {
  const res = await request.post('/api/decisions', {
    headers: { Authorization: bearer, ...JSON_HEADERS },
    data: {
      topicId: opts.topicId,
      meetingId: null,
      outcome: opts.outcome ?? 'Deferred',
      title: { en: opts.title, ar: opts.title },
      statement: { en: opts.statement, ar: opts.statement },
      rationale: { en: opts.rationale, ar: opts.rationale },
      alternatives: null,
      voteId: null,
      conditions: [],
    },
  });
  if (res.status() !== 201) throw new Error(`[e2e] record decision ${res.status()} ${await res.text()}`);
  return res.json();
}

/** Issue a drafted decision (→ Issued). Chairman-only (DecisionChairApprove). No override for a non-vote decision. */
export async function apiIssueDecision(request: APIRequestContext, bearer: string, decisionId: string): Promise<void> {
  const res = await request.post(`/api/decisions/${decisionId}/issue`, {
    headers: { Authorization: bearer, ...JSON_HEADERS },
    data: { chairOverride: false, overrideJustification: null },
  });
  if (!res.ok()) throw new Error(`[e2e] issue decision ${res.status()} ${await res.text()}`);
}

export interface ApiVote {
  id: string;
  key: string;
  status: string;
}

/** One eligible voter on the configure command (member sub + display-name snapshot). */
export interface VoteVoter {
  userId: string;
  name: string;
}

/**
 * Configure a vote via the API (P17b voting cluster). Stays `Configured` until opened. Defaults match the
 * common case (two options, cast quorum 1, no attendance quorum, no abstain); callers override per-AC.
 * Vote.Manage — the secretary's bearer suffices. Returns the vote id (for open/cast/close) + key (for the UI).
 */
export async function apiConfigureVote(
  request: APIRequestContext,
  bearer: string,
  opts: {
    topicId: string;
    eligibleVoters: VoteVoter[];
    options?: string[];
    minPresent?: number;
    minCast?: number;
    allowAbstain?: boolean;
    meetingId?: string;
  },
): Promise<ApiVote> {
  const res = await request.post('/api/votes', {
    headers: { Authorization: bearer, ...JSON_HEADERS },
    data: {
      topicId: opts.topicId,
      meetingId: opts.meetingId ?? null,
      options: opts.options ?? ['Approve', 'Reject'],
      allowAbstain: opts.allowAbstain ?? false,
      minPresent: opts.minPresent ?? 0,
      minCast: opts.minCast ?? 1,
      eligibleVoters: opts.eligibleVoters,
    },
  });
  if (res.status() !== 201) throw new Error(`[e2e] configure vote ${res.status()} ${await res.text()}`);
  return res.json();
}

/** Open a configured vote (Vote.Manage). Locks the configuration; casting becomes possible. */
export async function apiOpenVote(request: APIRequestContext, bearer: string, voteId: string): Promise<void> {
  const res = await request.post(`/api/votes/${voteId}/open`, { headers: { Authorization: bearer } });
  if (!res.ok()) throw new Error(`[e2e] open vote ${res.status()} ${await res.text()}`);
}

/**
 * Cast a ballot as the CURRENT user (Vote.Cast = Chairman/Member — the Secretary manages but does not vote,
 * so this must run with a voter's bearer). The caster must be one of the vote's eligible voters.
 */
export async function apiCastBallot(
  request: APIRequestContext,
  bearer: string,
  voteId: string,
  choice: string,
): Promise<void> {
  const res = await request.post(`/api/votes/${voteId}/cast`, {
    headers: { Authorization: bearer, ...JSON_HEADERS },
    data: { choice, comment: null },
  });
  if (!res.ok()) throw new Error(`[e2e] cast ballot ${res.status()} ${await res.text()}`);
}

/** Close an open vote (Vote.Manage). Throws server-side if cast count < MinCast (the AC-024 guard). */
export async function apiCloseVote(request: APIRequestContext, bearer: string, voteId: string): Promise<void> {
  const res = await request.post(`/api/votes/${voteId}/close`, { headers: { Authorization: bearer } });
  if (!res.ok()) throw new Error(`[e2e] close vote ${res.status()} ${await res.text()}`);
}

export interface ApiMinutes {
  id: string;
  key: string;
  version: number;
  status: string;
}

/** Start a Draft MoM for a (started/held) meeting. One MoM per meeting. Policy MinutesCapture. */
export async function apiDraftMinutes(
  request: APIRequestContext,
  bearer: string,
  meetingId: string,
  summary: string,
): Promise<ApiMinutes> {
  const res = await request.post('/api/minutes', {
    headers: { Authorization: bearer, ...JSON_HEADERS },
    data: { meetingId, summary: { en: summary, ar: summary } },
  });
  if (res.status() !== 201) throw new Error(`[e2e] draft minutes ${res.status()} ${await res.text()}`);
  return res.json();
}

/** Submit a Draft MoM for review (Draft → InReview). */
export async function apiSubmitMinutes(request: APIRequestContext, bearer: string, minutesId: string): Promise<void> {
  const res = await request.post(`/api/minutes/${minutesId}/submit`, { headers: { Authorization: bearer } });
  if (!res.ok()) throw new Error(`[e2e] submit minutes ${res.status()} ${await res.text()}`);
}

/** Approve an InReview MoM (→ Approved; soft SoD-2 off CreatedBy, non-blocking). Policy MinutesApprove. */
export async function apiApproveMinutes(request: APIRequestContext, bearer: string, minutesId: string): Promise<void> {
  const res = await request.post(`/api/minutes/${minutesId}/approve`, { headers: { Authorization: bearer } });
  if (!res.ok()) throw new Error(`[e2e] approve minutes ${res.status()} ${await res.text()}`);
}

/** Publish an Approved MoM (→ Published + notify). Policy MinutesApprove. */
export async function apiPublishMinutes(request: APIRequestContext, bearer: string, minutesId: string): Promise<void> {
  const res = await request.post(`/api/minutes/${minutesId}/publish`, { headers: { Authorization: bearer } });
  if (!res.ok()) throw new Error(`[e2e] publish minutes ${res.status()} ${await res.text()}`);
}

/** Publish a meeting's agenda (flips its topics to Scheduled + notifies) — a prerequisite of starting. */
export async function apiPublishAgenda(request: APIRequestContext, bearer: string, meetingId: string): Promise<void> {
  const res = await request.post(`/api/meetings/${meetingId}/agenda/publish`, { headers: { Authorization: bearer } });
  if (!res.ok()) throw new Error(`[e2e] publish agenda ${res.status()} ${await res.text()}`);
}

/** Start a meeting (→ in-session). Its agenda must be published first. */
export async function apiStartMeeting(request: APIRequestContext, bearer: string, meetingId: string): Promise<void> {
  const res = await request.post(`/api/meetings/${meetingId}/start`, { headers: { Authorization: bearer } });
  if (!res.ok()) throw new Error(`[e2e] start meeting ${res.status()} ${await res.text()}`);
}

/**
 * Mark a member Present for a meeting. `userId` is the member's publicId (the attendance identity); the
 * present-eligible count that gates a meeting-linked vote's open counts Present + IsVotingEligible rows.
 */
export async function apiMarkAttendance(
  request: APIRequestContext,
  bearer: string,
  meetingId: string,
  opts: { userId: string; name: string; role: string; isVotingEligible: boolean },
): Promise<void> {
  const res = await request.post(`/api/meetings/${meetingId}/attendance`, {
    headers: { Authorization: bearer, ...JSON_HEADERS },
    data: { userId: opts.userId, name: opts.name, role: opts.role, status: 'Present', isVotingEligible: opts.isVotingEligible },
  });
  if (!res.ok()) throw new Error(`[e2e] mark attendance ${res.status()} ${await res.text()}`);
}

/**
 * Trigger a native HTML5 drag→drop by dispatching the events directly. The app's drag handlers
 * store state in React refs/state on `dragstart` and read it on `drop` (no dataTransfer payload),
 * so a direct event dispatch is more deterministic than geometry-based mouse simulation.
 */
export async function dragHtml5(source: Locator, target: Locator): Promise<void> {
  await source.dispatchEvent('dragstart');
  await target.dispatchEvent('dragover');
  await target.dispatchEvent('drop');
  await source.dispatchEvent('dragend');
}
