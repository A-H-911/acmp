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
 * ⚠ The temporary password exists ONLY in this response. "No email in v1" is a hard constraint, so
 * the inviter reading it out is the delivery channel. It must never be logged, persisted, or put
 * anywhere it can be re-read: the 26-password CSV that had to be deleted by hand is the hazard this
 * repeats if it leaks. React Query caches mutation results in memory for the component's lifetime
 * only, and this is deliberately NOT written into the ['members'] cache.
 */
export function useInviteUser() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { email: string; fullName: string }) =>
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
