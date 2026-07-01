/*
 * Pull role-bearing claim strings out of a Keycloak ID/access-token profile.
 * Keycloak exposes realm roles under realm_access.roles, client roles under
 * resource_access[client].roles, and group memberships under groups. We gather
 * all candidates and let rolesFromClaims() normalize them (P4 finalizes the
 * exact mapper against the real realm).
 */
export interface OidcProfileLike {
  /** The OIDC subject — the caller's stable Keycloak user id (matches an action's OwnerUserId). */
  sub?: string;
  name?: string;
  preferred_username?: string;
  email?: string;
  groups?: string[];
  realm_access?: { roles?: string[] };
  resource_access?: Record<string, { roles?: string[] }>;
}

export function claimStringsFrom(profile: OidcProfileLike | undefined): string[] {
  if (!profile) return [];
  const out: string[] = [];
  if (profile.realm_access?.roles) out.push(...profile.realm_access.roles);
  if (profile.resource_access) {
    for (const client of Object.values(profile.resource_access)) {
      if (client.roles) out.push(...client.roles);
    }
  }
  if (profile.groups) out.push(...profile.groups);
  return out;
}

export function displayNameFrom(profile: OidcProfileLike | undefined): string {
  return profile?.name ?? profile?.preferred_username ?? profile?.email ?? 'User';
}

export function initialsFrom(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}
