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

  return (
    <nav className="sidebar" aria-label={t('nav.primary')}>
      {groups.map((group, gi) => (
        <div key={group.labelKey ?? `cta-${gi}`}>
          {group.labelKey && <div className="nav-group-label">{t(group.labelKey)}</div>}
          {group.items.map((item) => (
            <NavLink
              key={item.key}
              to={item.path}
              end={item.path === '/dashboard'}
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
    </nav>
  );
}
