/*
 * Administration → Notification Settings (mirrors the "ACMP Administration" `notif` section).
 * The channel cards state canonical facts (ADR-0005): in-app is the only v1 channel and is always on;
 * Email and Webex are Phase-2 planned. The per-event matrix shows the v1 in-app defaults as a
 * READ-ONLY reference — persisting per-event overrides is a P15 surface (no settings store yet), so
 * the toggles are presentational (recorded deviation), and Email/Webex columns read "Planned".
 */
import { useTranslation } from 'react-i18next';
import { StatusChip } from '../../components/ui/StatusChip';
import { Table, type Column } from '../../components/ui/Table';
import { Icon, type IconName } from '../../components/icons';

const CHANNELS: { key: string; icon: IconName; active: boolean }[] = [
  { key: 'inapp', icon: 'bell', active: true },
  { key: 'email', icon: 'mail', active: false },
  { key: 'webex', icon: 'video', active: false },
];

// Default in-app notifications by event type (design evRaw). ADR published is off by default.
const EVENTS: { key: string; on: boolean }[] = [
  { key: 'topicSubmitted', on: true },
  { key: 'topicReturned', on: true },
  { key: 'agendaPublished', on: true },
  { key: 'meetingScheduled', on: true },
  { key: 'minutesReady', on: true },
  { key: 'decisionApproved', on: true },
  { key: 'voteOpened', on: true },
  { key: 'actionAssigned', on: true },
  { key: 'actionOverdue', on: true },
  { key: 'riskAssigned', on: true },
  { key: 'adrPublished', on: false },
];

export function NotificationSettings() {
  const { t } = useTranslation();

  const columns: Column<{ key: string; on: boolean }>[] = [
    { id: 'event', header: t('admin.notif.col.event'), width: '52%', cell: (e) => <span className="adm-notif-event">{t(`admin.notif.event.${e.key}`)}</span> },
    {
      id: 'inapp',
      header: t('admin.notif.col.inApp'),
      width: '16%',
      cell: (e) => (
        <span
          className="adm-switch"
          role="switch"
          aria-checked={e.on}
          aria-disabled="true"
          aria-label={t(`admin.notif.event.${e.key}`)}
        >
          <span className="adm-knob" aria-hidden="true" />
        </span>
      ),
    },
    { id: 'email', header: t('admin.notif.col.email'), width: '16%', cell: () => <span className="adm-planned">{t('admin.notif.planned')}</span> },
    { id: 'webex', header: t('admin.notif.col.webex'), width: '16%', cell: () => <span className="adm-planned">{t('admin.notif.planned')}</span> },
  ];

  return (
    <>
      <div className="adm-notif-channels">
        {CHANNELS.map((c) => (
          <div key={c.key} className={`adm-tile${c.active ? '' : ' adm-tile-muted'}`}>
            <div className="adm-tile-head">
              <span className="adm-tile-title">
                <span className={`adm-tile-icon${c.active ? ' adm-tile-icon-on' : ''}`} aria-hidden="true">
                  <Icon name={c.icon} size={17} />
                </span>
                {t(`admin.notif.channel.${c.key}.name`)}
              </span>
              <StatusChip tone={c.active ? 'success' : 'neutral'} label={c.active ? t('admin.notif.channel.active') : t('admin.notif.channel.planned')} size="sm" />
            </div>
            <p className="adm-tile-desc">{t(`admin.notif.channel.${c.key}.desc`)}</p>
            <div className="adm-vote">
              <span className="adm-switch" role="switch" aria-checked={c.active} aria-disabled="true" aria-label={t(`admin.notif.channel.${c.key}.name`)}>
                <span className="adm-knob" aria-hidden="true" />
              </span>
              <span className={c.active ? 'adm-vote-on' : 'adm-vote-off'}>{c.active ? t('admin.notif.on') : t('admin.notif.off')}</span>
            </div>
          </div>
        ))}
      </div>

      <h2 className="adm-notif-matrix-title">{t('admin.notif.matrixTitle')}</h2>
      <p className="adm-notif-matrix-sub">{t('admin.notif.matrixSub')}</p>
      <Table caption={t('admin.notif.matrixTitle')} columns={columns} rows={EVENTS} getRowKey={(e) => e.key} />
    </>
  );
}
