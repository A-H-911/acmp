/*
 * DEV-only role switcher for previewing role-filtered navigation without a live
 * Keycloak. Renders only when the auth backend exposes devSetRoles (the DEV
 * stub) — it is absent from the production bundle, so it can never change a real
 * user's authorization (guardrail 4).
 */
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../auth/AcmpAuthContext';
import { COMMITTEE_ROLES, type CommitteeRole } from '../../auth/roles';

export function DevRoleSwitcher() {
  const { t } = useTranslation();
  const { roles, devSetRoles } = useAuth();
  if (!devSetRoles) return null;

  return (
    <label className="chip-btn" style={{ gap: 6 }}>
      <span className="visually-hidden">{t('dev.role')}</span>
      <span aria-hidden="true" style={{ color: 'var(--text-3)' }}>{t('dev.roleShort')}</span>
      <select
        value={roles[0] ?? 'secretary'}
        onChange={(e) => devSetRoles([e.target.value as CommitteeRole])}
        style={{ border: 0, background: 'transparent', color: 'var(--text)', font: 'inherit', fontWeight: 600 }}
      >
        {COMMITTEE_ROLES.map((r) => (
          <option key={r} value={r}>{t(`role.${r}`)}</option>
        ))}
      </select>
    </label>
  );
}
