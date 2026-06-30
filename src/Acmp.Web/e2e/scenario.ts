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
