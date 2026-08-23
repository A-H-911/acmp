/*
 * Member directory server state (AC-059). The directory is readable by any authenticated role; the
 * Administration screen that hosts it is itself admin-gated by route.
 *
 * NO LONGER READ-ONLY. Identities still live in Keycloak (ADR-0004), but ADR-0038 lets an
 * Administrator or Secretary create one from inside ACMP (FR-156) and set its committee role
 * (FR-157), so the mutations below write THROUGH to Keycloak rather than making this a second
 * identity store. The server enforces who may call them; this module never decides that.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './apiClient';

// DEF-046: minimal-API body binding answers 415 Unsupported Media Type without this — the invite
// below shipped in #235 missing it and could never have succeeded against a real server. Every
// other JSON mutation in this layer already carries it; this module was the only one that did not.
const JSON_HEADERS = { 'Content-Type': 'application/json' } as const;

export interface StreamRef {
  publicId: string;
  code: string;
  nameEn: string;
  nameAr: string;
  /**
   * ADR-0042's "unrestricted" marker — a member holding this stream is not stream-bounded.
   *
   * ⚠ Read this FLAG, never compare `code` to a literal. The server marks the wildcard with a
   * boolean column precisely so renaming or re-coding the row cannot silently break the meaning,
   * and a client that string-matched would reintroduce exactly that fragility.
   */
  isWildcard: boolean;
}

/** Which of a stream's two names to show. Both are required server-side (guardrail 9). */
export function streamName(s: Pick<StreamRef, 'nameEn' | 'nameAr'>, isArabic: boolean): string {
  return isArabic ? s.nameAr : s.nameEn;
}

/**
 * The committee's stream taxonomy (ADR-0042 step 1) — seeded reference data, not user-generated.
 * Readable by any authenticated role, like the rest of the directory.
 */
export function useStreams() {
  return useQuery({
    queryKey: ['streams'],
    queryFn: () => api<StreamRef[]>('/members/streams'),
    // It changes when a migration runs, not while someone is filling in a form.
    staleTime: 5 * 60 * 1000,
  });
}

/**
 * The streams a TOPIC may claim — everything except the wildcard (ADR-0042 clause 4), which says a
 * PERSON is unrestricted and is meaningless as a property of a topic.
 *
 * ⚠ This filter is a display convenience, NOT the enforcement. The server rejects the wildcard and
 * any unknown code in SubmitTopicValidator/UpdateTopicValidator; omitting it here only stops a user
 * picking something that would be refused. A hidden option has never been what makes a rule true.
 */
export function useAssignableStreams() {
  const query = useStreams();
  return { ...query, data: query.data?.filter((s) => !s.isWildcard) };
}

export interface Member {
  publicId: string;
  /** The member's Keycloak subject — used to assign work (e.g. an action's OwnerUserId). */
  keycloakUserId: string;
  fullName: string;
  email: string;
  role: string;
  status: string;
  isActive: boolean;
  isVotingEligible: boolean;
  streams: StreamRef[];
}

export function useMembers() {
  return useQuery({
    queryKey: ['members'],
    queryFn: () => api<Member[]>('/members'),
  });
}

/**
 * Assign a member's streams (BL-024 / ADR-0042 step 3). Administrator only — enforced server-side.
 *
 * ⚠ The body is a BARE ARRAY of stream PublicIds, not a wrapped object: that is what the minimal-API
 * endpoint binds (`PUT /api/members/{publicId}/streams`). It REPLACES the member's assignments
 * rather than adding to them, which is what makes deselecting a stream possible at all.
 *
 * ⚠ The server IGNORES unknown ids rather than refusing them (AssignStreamsHandler), so a caller
 * that sent a stale id would see a silent partial save. This client only ever sends ids it just read
 * from GET /members/streams, which is why that is tolerable here and would not be from a script.
 */
export function useAssignStreams() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ publicId, streamPublicIds }: { publicId: string; streamPublicIds: string[] }) =>
      api<void>(`/members/${publicId}/streams`, {
        method: 'PUT',
        headers: JSON_HEADERS,
        body: JSON.stringify(streamPublicIds),
      }),
    // The roster renders each member's stream chips, so without this the directory keeps showing the
    // old set and the change reads as having silently failed.
    onSuccess: () => qc.invalidateQueries({ queryKey: ['members'] }),
  });
}

/**
 * Set a member's voting eligibility — Chairman or Secretary (DEF-041 / DEC-046 d4).
 *
 * ⚠ The DESIRED STATE is sent, never a "toggle" instruction. Two clicks racing each other would
 * otherwise resolve to whichever order the server happened to see last, and neither caller could
 * tell what they had set.
 *
 * ⚠ Administrator is refused by the server (SoD-5 keeps that role out of committee content), so a
 * 403 here is the matrix working rather than a bug. Hiding the control for other roles is
 * presentation gating only — the API is what enforces it.
 */
export function useSetVotingEligibility() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ publicId, isVotingEligible }: { publicId: string; isVotingEligible: boolean }) =>
      api<void>(`/members/${publicId}/voting-eligibility`, {
        method: 'PUT',
        headers: JSON_HEADERS,
        body: JSON.stringify({ isVotingEligible }),
      }),
    // The directory renders the switch from this data, so without the refetch the control springs
    // back to its old position and the change reads as having failed.
    onSuccess: () => qc.invalidateQueries({ queryKey: ['members'] }),
  });
}

/**
 * The invited account. `temporaryPassword` is returned by the server EXACTLY ONCE (AC-088).
 */
export interface InvitedUser {
  publicId: string;
  fullName: string;
  email: string;
  status: string;
  temporaryPassword: string;
}

/**
 * Invite a user (FR-156). Administrator or Secretary only — enforced server-side.
 *
 * ⚠ streamPublicIds is REQUIRED by the server (ADR-0043 clause 2): an invite with no stream
 * would create a member who, once step 7 wires stream scope, can write nothing at all.
 *
 * ⚠ The temporary password exists ONLY in this response. "No email in v1" is a hard constraint, so
 * the inviter reading it out is the delivery channel. It must never be logged, persisted, or put
 * anywhere it can be re-read: the 26-password CSV that had to be deleted by hand is the hazard this
 * repeats if it leaks. React Query caches mutation results in memory for the component's lifetime
 * only, and this is deliberately NOT written into the ['members'] cache.
 */
export function useInviteUser() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { email: string; fullName: string; streamPublicIds: string[] }) =>
      api<InvitedUser>('/members/invite', { method: 'POST', headers: JSON_HEADERS, body: JSON.stringify(body) }),
    // The new member arrives at status Invited, so the roster must refetch to show it.
    onSuccess: () => qc.invalidateQueries({ queryKey: ['members'] }),
  });
}

/**
 * Assign a member's committee roles (FR-157 / AC-089). Administrator or Secretary — enforced
 * server-side, never here.
 *
 * ⚠ `confirmedPrivileged` is NOT a UI flag the server trusts blindly — it is the server's own gate:
 * a request whose role set contains Administrator or Chairman is REFUSED without it (guard 2). The
 * caller must therefore have obtained a real confirmation before setting it, and the set is sent
 * whole: this REPLACES the person's realm roles rather than adding to them.
 *
 * The server also refuses a self-role-change and any change that would leave the committee with no
 * Administrator. Those refusals are surfaced to the user, not pre-empted by hiding the control —
 * a hidden control is presentation gating, and it is never what stops a refused change.
 */
export function useAssignRoles() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ publicId, roles, confirmedPrivileged }: { publicId: string; roles: string[]; confirmedPrivileged: boolean }) =>
      api<void>(`/members/${publicId}/roles`, {
        method: 'PUT',
        headers: JSON_HEADERS,
        body: JSON.stringify({ roles, confirmedPrivileged }),
      }),
    // The roster caches the primary role per row, so without this the directory keeps showing the
    // old one and the change reads as having silently failed.
    onSuccess: () => qc.invalidateQueries({ queryKey: ['members'] }),
  });
}
