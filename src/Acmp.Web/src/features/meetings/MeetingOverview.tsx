/*
 * Meeting overview (P6a) — the index child of the meeting shell (ACMP Meetings.dc.html isOverview).
 * The shell already renders the header card + tab strip; this is the landing body:
 *   conditional lifecycle banner → grid[ agenda-preview card | readiness rows + quick-links ].
 *
 * The banner moved here from above the tabs (round-1) so the shell chrome stays the same on every
 * sub-page. Reads the cached detail + members (react-query dedupes with the shell). Quorum is a
 * display heuristic only — the authoritative gate is the Voting phase (P9).
 */
import { useParams, Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useMeetingDetail } from '../../api/meetings';
import { useMembers } from '../../api/members';
import { Icon, type IconName } from '../../components/icons';
import { MeetingGate } from './MeetingGate';
import { AgendaPreview } from './AgendaBuilder';
import { lifecyclePhase, type MeetingPhase } from './meetingStatus';

const BANNER_ICON: Record<Exclude<MeetingPhase, 'ready'>, IconName> = {
  notReady: 'warnTriangle',
  inProgress: 'clock',
  concluded: 'checkCircle',
  cancelled: 'x',
};

/** Quick-link targets, relative to the meeting shell (the index shares the parent's path). */
const QUICK_LINKS: { to: string; icon: IconName; labelKey: string }[] = [
  { to: 'agenda', icon: 'backlog', labelKey: 'meetings.overview.linkAgenda' },
  { to: 'notes', icon: 'doc', labelKey: 'meetings.overview.linkConduct' },
  { to: 'minutes', icon: 'adr', labelKey: 'meetings.overview.linkMinutes' },
  { to: 'recording', icon: 'video', labelKey: 'meetings.overview.linkRecording' },
];

export function MeetingOverview() {
  const { key } = useParams();
  const { t } = useTranslation();
  const { data: meeting } = useMeetingDetail(key);
  const { data: members } = useMembers();
  if (!meeting) return null; // shell owns loading/error.

  const agenda = meeting.agenda;
  const items = agenda?.items ?? [];
  const agendaPublished = agenda?.status === 'Published' || agenda?.status === 'Locked';
  const phase = lifecyclePhase(meeting.status, agendaPublished);

  const votingEligible = (members ?? []).filter((m) => m.isActive && m.isVotingEligible);
  const quorumNeeded = votingEligible.length > 0 ? Math.floor(votingEligible.length / 2) + 1 : 0;

  const readiness = [
    { id: 'agenda', label: t('meetings.overview.agendaPublished'), met: agendaPublished },
    { id: 'topics', label: t('meetings.overview.topicsScheduled'), met: items.length > 0, hint: t('meetings.itemCount', { count: items.length }) },
    { id: 'quorum', label: t('meetings.overview.quorumExpected'), hint: t('meetings.overview.quorumOf', { needed: quorumNeeded, total: votingEligible.length }) },
  ];

  return (
    <div className="mt-overview">
      {phase !== 'ready' && (
        <div className={`mt-lifecycle mt-lifecycle-${phase}`} role="status">
          <span className="mt-lifecycle-icon" aria-hidden="true">
            <Icon name={BANNER_ICON[phase]} size={18} />
          </span>
          <div className="mt-lifecycle-text">
            <p className="mt-lifecycle-title">{t(`meetings.banner.${phase}.title`)}</p>
            <p className="mt-lifecycle-body">{t(`meetings.banner.${phase}.body`)}</p>
          </div>
        </div>
      )}

      <div className="mt-overview-grid">
        <div className="mt-overview-main">
          {items.length > 0 ? (
            <AgendaPreview items={items} usedMinutes={agenda?.totalTimeboxMinutes ?? 0} />
          ) : (
            <MeetingGate icon="viewKanban" title={t('meetings.overview.noAgenda.title')} body={t('meetings.overview.noAgenda.body')} />
          )}
        </div>

        <aside className="mt-overview-side">
          <section className="mt-readiness" aria-label={t('meetings.overview.readinessTitle')}>
            <h2 className="mt-col-title">{t('meetings.overview.readinessTitle')}</h2>
            <ul className="mt-readiness-list">
              {readiness.map((row) => (
                <li key={row.id} className="mt-readiness-row">
                  {'met' in row ? (
                    <span className={`mt-readiness-mark ${row.met ? 'met' : 'unmet'}`} aria-hidden="true">
                      <Icon name={row.met ? 'checkCircle' : 'alertCircle'} size={16} />
                    </span>
                  ) : (
                    <span className="mt-readiness-mark info" aria-hidden="true">
                      <Icon name="usersGroup" size={16} />
                    </span>
                  )}
                  <span className="mt-readiness-label">{row.label}</span>
                  {row.hint && <span className="mt-readiness-hint">{row.hint}</span>}
                </li>
              ))}
            </ul>
          </section>

          <section className="mt-quicklinks" aria-label={t('meetings.overview.quickLinksTitle')}>
            <h2 className="mt-col-title">{t('meetings.overview.quickLinksTitle')}</h2>
            <ul className="mt-quicklinks-list">
              {QUICK_LINKS.map((link) => (
                <li key={link.to}>
                  <Link className="mt-quicklink" to={link.to}>
                    <Icon name={link.icon} size={15} aria-hidden />
                    <span>{t(link.labelKey)}</span>
                    <Icon name="chevron" size={14} className="mt-quicklink-chevron" aria-hidden />
                  </Link>
                </li>
              ))}
            </ul>
          </section>
        </aside>
      </div>
    </div>
  );
}
