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
