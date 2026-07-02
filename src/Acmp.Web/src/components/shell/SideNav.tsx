/*
 * Role-filtered primary navigation (docs/14 §2.1). Renders only the groups and
 * areas the current roles can reach; "view" access shows a read-only eye marker.
 * Labels are i18n keys; the sidebar mirrors to the inline-end edge in RTL via
 * logical properties.
 */
import { useMemo } from 'react';
import { NavLink } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../auth/AcmpAuthContext';
import { buildNav } from '../../nav/navModel';
import { Icon } from '../icons';

export function SideNav() {
  const { t } = useTranslation();
  const { roles } = useAuth();
  const groups = useMemo(() => buildNav(roles), [roles]);
  // A path that prefixes another nav path (e.g. /admin vs /admin/audit) must match
  // exactly, or both items light up as active. Compute that set once.
  const allPaths = useMemo(() => groups.flatMap((g) => g.items.map((i) => i.path)), [groups]);
  const exactMatch = (path: string) =>
    path === '/' || allPaths.some((p) => p !== path && p.startsWith(`${path}/`));

  const primaryRole = roles[0] ?? 'Guest';

  return (
    <nav className="sidebar" aria-label={t('nav.primary')}>
      <div className="nav-roleview">
        <span className="nav-roleview-dot" aria-hidden="true" />
        <span className="nav-roleview-text">
          {t('nav.viewingAs')} <b>{t(`roles.${primaryRole}`, primaryRole)}</b>
        </span>
      </div>
      {groups.map((group, gi) => (
        <div key={group.labelKey ?? `cta-${gi}`}>
          {group.labelKey && <div className="nav-group-label">{t(group.labelKey)}</div>}
          {group.items.map((item) => (
            <NavLink
              key={item.key}
              to={item.path}
              end={exactMatch(item.path)}
              className={({ isActive }) => `nav-item ${item.cta ? 'cta' : ''} ${isActive ? 'active' : ''}`}
            >
              <Icon name={item.icon} size={17} />
              <span className="nav-item-label">{t(item.labelKey)}</span>
              {item.access === 'view' && !item.cta && (
                <span className="nav-item-viewonly" title={t('nav.readOnly')}>
                  <Icon name="eye" size={14} aria-hidden={false} aria-label={t('nav.readOnly')} />
                </span>
              )}
            </NavLink>
          ))}
        </div>
      ))}
      <div className="nav-audit-card">
        <Icon name="lock" size={15} aria-hidden />
        <div>
          <div className="nav-audit-title">{t('nav.auditOn')}</div>
          <div className="nav-audit-sub">{t('nav.auditSub')}</div>
        </div>
      </div>
    </nav>
  );
}
