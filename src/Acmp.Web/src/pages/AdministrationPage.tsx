/*
 * Administration area (admin-gated by route; mirrors "ACMP Administration.dc.html"). Owns the page
 * header + the tab strip and renders the active tab's body. The design marks no tab disabled, so
 * every tab is navigable (Usage Map): System Health, Roles, and Job Monitor are live/canonical;
 * Notification Settings is canonical read-only; Streams is honest-empty because its module data
 * lands in a later phase (P15).
 *
 * ⚠ USERS IS NO LONGER A TAB HERE (OQ-069, resolved by DEC-041; divergence recorded as SC-008).
 * FR-156 and FR-157 both read "As an Administrator or Secretary" and the server has always honoured
 * it, but this route is Administrator-only, so the affordances were unreachable for half the roles
 * the requirements name. The roster, the invite and role assignment moved to /members, which admits
 * both. Widening THIS route instead would have exposed system health, the job monitor, streams and
 * notification settings to the Secretary (permission-role-matrix row 27, SoD-5).
 */
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Tabs } from '../components/ui/Tabs';
import { Icon, type IconName } from '../components/icons';
import { SystemHealth } from '../features/administration/SystemHealth';
import { RolesReference } from '../features/administration/RolesReference';
import { NotificationSettings } from '../features/administration/NotificationSettings';
import { JobMonitor } from '../features/administration/JobMonitor';
import { ComingDataTab } from '../features/administration/ComingDataTab';
import '../styles/administration.css';

// The Administration sub-tabs, in design order. Templates moved to the standalone /templates route
// (P15e) in the Knowledge nav group, so it is no longer an Admin tab.
const TABS: { id: string; icon: IconName }[] = [
  { id: 'health', icon: 'activity' },
  { id: 'streams', icon: 'stream' },
  { id: 'roles', icon: 'shieldUser' },
  { id: 'jobs', icon: 'cog' },
  { id: 'notifications', icon: 'bell' },
];

export default function AdministrationPage() {
  const { t } = useTranslation();
  const [sub, setSub] = useState('health');

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

      {sub === 'health' && <SystemHealth />}
      {sub === 'roles' && <RolesReference />}
      {sub === 'notifications' && <NotificationSettings />}
      {sub === 'streams' && <ComingDataTab tab="streams" icon="stream" />}
      {sub === 'jobs' && <JobMonitor />}
    </section>
  );
}
