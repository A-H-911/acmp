/*
 * DEV-only role switcher for previewing role-filtered navigation without a live
 * Keycloak. Renders only when the auth backend exposes devSetRoles (the DEV
 * stub) — absent from the production bundle, so it can never change a real
 * user's authorization (guardrail 4).
 *
 * Styled to MATCH the design's role menu (Navigation & IA: 210px trigger →
 * 280px radio menu with avatars + a "read-only from Keycloak" header). Behaviour
 * stays dev-only: matching the appearance does not make role-switching shippable.
 */
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../auth/AcmpAuthContext';
import { COMMITTEE_ROLES } from '../../auth/roles';
import { Icon } from '../icons';

function roleInitials(label: string): string {
  const p = label.trim().split(/\s+/);
  return ((p[0]?.[0] ?? '') + (p[1]?.[0] ?? '')).toUpperCase() || '?';
}

export function DevRoleSwitcher() {
  const { t } = useTranslation();
  const { roles, devSetRoles } = useAuth();
  const [open, setOpen] = useState(false);

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setOpen(false);
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [open]);

  if (!devSetRoles) return null;

  const current = roles[0] ?? 'secretary';
  const currentLabel = t(`role.${current}`);

  return (
    <div className="role-menu">
      <button
        type="button"
        className="role-menu-trigger"
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={() => setOpen((o) => !o)}
      >
        <span className="role-avatar" aria-hidden="true">{roleInitials(currentLabel)}</span>
        <span className="role-menu-id">
          <span className="role-menu-label">{currentLabel}</span>
          <span className="role-menu-sub">{t('dev.role')}</span>
        </span>
        <Icon name="chevronDown" size={15} className="role-menu-caret" aria-hidden />
      </button>

      {open && (
        <>
          <div className="profile-backdrop" onClick={() => setOpen(false)} aria-hidden="true" />
          <div className="role-menu-panel" role="menu" aria-label={t('dev.role')}>
            <div className="role-menu-head">{t('dev.rolesHeader')}</div>
            {COMMITTEE_ROLES.map((r) => (
              <button
                key={r}
                type="button"
                role="menuitemradio"
                aria-checked={r === current}
                className="role-menu-item"
                onClick={() => { devSetRoles([r]); setOpen(false); }}
              >
                <span className="role-avatar" aria-hidden="true">{roleInitials(t(`role.${r}`))}</span>
                <span className="role-menu-item-label">{t(`role.${r}`)}</span>
                {r === current && <Icon name="check" size={15} className="role-menu-check" aria-hidden />}
              </button>
            ))}
          </div>
        </>
      )}
    </div>
  );
}
