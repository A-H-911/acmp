/*
 * Administration area (admin-gated by route; mirrors "ACMP Administration.dc.html"). Owns the page
 * header + the 7-tab strip and renders the active tab's body. The design marks no tab disabled, so
 * every tab is navigable (Usage Map): Users is fully wired; System Health and Roles are live/canonical;
 * Notification Settings is canonical read-only; Templates / Streams / Job Monitor are honest-empty
 * because their module data lands in later phases (P14/P15). Opening a user's detail replaces the
 * tabbed view (the design's `userdetail` sub-state).
 */
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Tabs } from '../components/ui/Tabs';
import { Icon, type IconName } from '../components/icons';
import type { Member } from '../api/members';
import { UsersDirectory, UserDetail } from '../features/administration/UsersMembership';
import { SystemHealth } from '../features/administration/SystemHealth';
import { RolesReference } from '../features/administration/RolesReference';
import { NotificationSettings } from '../features/administration/NotificationSettings';
import { ComingDataTab } from '../features/administration/ComingDataTab';
import '../styles/administration.css';

// The seven Administration sub-tabs, in design order.
const TABS: { id: string; icon: IconName }[] = [
  { id: 'users', icon: 'usersGroup' },
  { id: 'templates', icon: 'template' },
  { id: 'health', icon: 'activity' },
  { id: 'streams', icon: 'stream' },
  { id: 'roles', icon: 'shieldUser' },
  { id: 'jobs', icon: 'cog' },
  { id: 'notifications', icon: 'bell' },
];

export default function AdministrationPage() {
  const { t, i18n } = useTranslation();
  const [sub, setSub] = useState('users');
  const [detail, setDetail] = useState<Member | null>(null);

  if (detail) {
    return <UserDetail member={detail} isArabic={i18n.language === 'ar'} onBack={() => setDetail(null)} />;
  }

  const tabs = TABS.map(({ id, icon }) => ({
    id,
    label: (
      <>
        <Icon name={icon} size={16} aria-hidden />
        {t(`admin.tabs.${id}`)}
      </>
    ),
  }));

  return (
    <section className="page">
      <div className="adm-head">
        <div>
          <h1 className="page-title">{t('admin.title')}</h1>
          <div className="adm-head-sub">{t(`admin.sub.${sub}`)}</div>
        </div>
      </div>

      <Tabs items={tabs} value={sub} onValueChange={setSub} ariaLabel={t('admin.title')} />

      {sub === 'users' && <UsersDirectory onView={setDetail} />}
      {sub === 'health' && <SystemHealth />}
      {sub === 'roles' && <RolesReference />}
      {sub === 'notifications' && <NotificationSettings />}
      {sub === 'templates' && <ComingDataTab tab="templates" icon="template" />}
      {sub === 'streams' && <ComingDataTab tab="streams" icon="stream" />}
      {sub === 'jobs' && <ComingDataTab tab="jobs" icon="cog" />}
    </section>
  );
}
