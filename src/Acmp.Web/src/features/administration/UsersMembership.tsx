/*
 * Administration → Users & Membership (mirrors the "ACMP Administration" design file — the Users
 * sub-tab + the read-only user-detail drill-down). Composes the shared component library (Tabs,
 * Table, StatusChip, Icon, states); only the domain-specific directory cells carry local CSS.
 *
 * Behavior (read-only this phase, per ADR-0004/0015 — identities + roles come from Keycloak):
 *  - The 7-tab strip matches the design; only Users & Membership is implemented. The other six
 *    (Templates, System Health, Streams, Roles, Job Monitor, Notification Settings) are disabled
 *    placeholders for their later phases (no-reference-yet here — flagged in the progress log).
 *  - Membership editing affordances (committee × remove, dashed + add, voting-eligibility switch)
 *    are rendered to match the design but are INERT/disabled: stream assignment lands with BL-024
 *    and voting eligibility with Voting (P9). The directory stays read-only (GET /api/members).
 *  - The row's view button opens an in-place, read-only user detail (no routing — mirrors the
 *    design's `sub` state). The design's invite / "Provision via Keycloak" panel is intentionally
 *    NOT built: it conflicts with ADR-0015 (manual Keycloak provisioning, no in-app account
 *    creation) — see OQ in the progress log.
 * Wired to GET /api/members (AC-059).
 */
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMembers, type Member } from '../../api/members';
import { StatusChip, type StatusTone } from '../../components/ui/StatusChip';
import { LoadingState, ErrorState, EmptyState } from '../../components/states';
import { Tabs } from '../../components/ui/Tabs';
import { Table, type Column } from '../../components/ui/Table';
import { Icon, type IconName } from '../../components/icons';
import '../../styles/administration.css';

const STATUS_TONE: Record<string, StatusTone> = {
  Active: 'success',
  Invited: 'info',
  Disabled: 'neutral',
};

// The seven Administration sub-tabs, in design order. Only `users` is implemented this phase.
const TABS: { id: string; icon: IconName }[] = [
  { id: 'users', icon: 'usersGroup' },
  { id: 'templates', icon: 'template' },
  { id: 'health', icon: 'activity' },
  { id: 'streams', icon: 'stream' },
  { id: 'roles', icon: 'shieldUser' },
  { id: 'jobs', icon: 'cog' },
  { id: 'notifications', icon: 'bell' },
];

function initials(name: string): string {
  const parts = name.trim().split(/\s+/);
  return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || '?';
}

function streamName(s: { nameEn: string; nameAr: string }, isArabic: boolean): string {
  return isArabic ? s.nameAr : s.nameEn;
}

export function UsersMembership() {
  const { t, i18n } = useTranslation();
  const { data, isLoading, isError, refetch } = useMembers();
  const isArabic = i18n.language === 'ar';
  const [detail, setDetail] = useState<Member | null>(null);

  const tabs = TABS.map(({ id, icon }) => ({
    id,
    disabled: id !== 'users',
    label: (
      <>
        <Icon name={icon} size={16} aria-hidden />
        {t(`admin.tabs.${id}`)}
      </>
    ),
  }));

  if (detail) {
    return <UserDetail member={detail} isArabic={isArabic} onBack={() => setDetail(null)} />;
  }

  return (
    <section className="page">
      <div className="adm-head">
        <div>
          <h1 className="page-title">{t('admin.title')}</h1>
          <div className="adm-head-sub">{t('admin.subtitle')}</div>
        </div>
      </div>

      <Tabs items={tabs} value="users" onValueChange={() => {}} ariaLabel={t('admin.title')} />

      {isLoading ? (
        <LoadingState />
      ) : isError ? (
        <ErrorState onRetry={() => refetch()} />
      ) : !data || data.length === 0 ? (
        <EmptyState title={t('admin.emptyTitle')} body={t('admin.emptyBody')} icon="usersGroup" />
      ) : (
        <Directory members={data} isArabic={isArabic} onView={setDetail} />
      )}
    </section>
  );
}

function Directory({
  members,
  isArabic,
  onView,
}: {
  members: Member[];
  isArabic: boolean;
  onView: (m: Member) => void;
}) {
  const { t } = useTranslation();

  const columns: Column<Member>[] = [
    {
      id: 'user',
      header: t('admin.col.user'),
      width: '30%',
      cell: (m) => (
        <span className="adm-user">
          <span className="adm-avatar" aria-hidden="true">
            {initials(m.fullName)}
          </span>
          <span style={{ minInlineSize: 0 }}>
            <span className="adm-name">{m.fullName}</span>
            <span className="adm-email" dir="ltr">
              {m.email}
            </span>
          </span>
        </span>
      ),
    },
    {
      id: 'role',
      header: t('admin.col.role'),
      width: '16%',
      cell: (m) => (
        <span className="adm-role">
          <span className="adm-role-name">{t(`role.${m.role.toLowerCase()}`)}</span>
          <span className="adm-lock">
            <Icon name="lock" size={11} aria-hidden />
            {t('admin.fromKeycloak')}
          </span>
        </span>
      ),
    },
    {
      id: 'membership',
      header: t('admin.col.membership'),
      width: '28%',
      cell: (m) => (
        <span className="adm-membership">
          <span className="adm-chips">
            {m.streams.length === 0 ? (
              <span className="adm-mchip adm-observer">{t('admin.observer')}</span>
            ) : (
              m.streams.map((s) => (
                <span key={s.publicId} className="adm-mchip">
                  {streamName(s, isArabic)}
                  <Icon name="x" size={10} aria-hidden />
                </span>
              ))
            )}
            {/* Add-committee: editing lands with stream assignment (BL-024) — inert this phase. */}
            <button type="button" className="adm-add" aria-label={t('admin.addCommittee')} disabled>
              <Icon name="plus" size={12} aria-hidden />
            </button>
          </span>
          <span className="adm-vote">
            <span
              className="adm-switch"
              role="switch"
              aria-checked={m.isVotingEligible}
              aria-disabled="true"
              aria-label={t('admin.votingEligible')}
            >
              <span className="adm-knob" aria-hidden="true" />
            </span>
            <span className={m.isVotingEligible ? 'adm-vote-on' : 'adm-vote-off'}>{t('admin.votingEligible')}</span>
          </span>
        </span>
      ),
    },
    {
      id: 'assignments',
      header: t('admin.col.assignments'),
      width: '11%',
      // No assignment count on the member API yet (topic/action modules) — honest dash + tooltip.
      cell: () => (
        <span className="adm-assign" title={t('admin.assignmentsHint')}>
          <Icon name="check" size={13} aria-hidden />—
        </span>
      ),
    },
    {
      id: 'status',
      header: t('admin.col.status'),
      width: '15%',
      cell: (m) => (
        <span className="adm-status">
          <StatusChip tone={STATUS_TONE[m.status] ?? 'neutral'} label={t(`admin.status.${m.status.toLowerCase()}`)} size="sm" />
          <button type="button" className="adm-view" aria-label={t('admin.viewUser')} onClick={() => onView(m)}>
            <Icon name="chevron" size={15} aria-hidden />
          </button>
        </span>
      ),
    },
  ];

  return (
    <>
      <div className="adm-banner">
        <Icon name="infoCircle" size={17} aria-hidden />
        <div>
          <b>{t('admin.kc.title')}</b> — {t('admin.kc.note')}
        </div>
      </div>

      <div className="adm-filters">
        {(['role', 'status', 'membership'] as const).map((f) => (
          <button key={f} type="button" className="adm-filter" disabled>
            {t(`admin.filter.${f}`)}
            <Icon name="chevronDown" size={12} aria-hidden />
          </button>
        ))}
        <span className="adm-count">{t('admin.showing', { count: members.length })}</span>
      </div>

      <Table caption={t('admin.tabs.users')} columns={columns} rows={members} getRowKey={(m) => m.publicId} />
    </>
  );
}

/**
 * Read-only user detail (the design's user-detail panel, minus the invite section). Renders only
 * data the member API returns — Keycloak ID / last sign-in / provisioned date are omitted until the
 * directory exposes them. No editing, no invite (ADR-0015).
 */
function UserDetail({ member, isArabic, onBack }: { member: Member; isArabic: boolean; onBack: () => void }) {
  const { t } = useTranslation();
  return (
    <section className="page">
      <div className="adm-detail-back">
        <button type="button" className="adm-back" onClick={onBack}>
          <Icon name="chevron" size={15} aria-hidden />
          {t('admin.detail.back')}
        </button>
      </div>

      <div className="adm-detail-card">
        <div className="adm-detail-head">
          <span className="adm-avatar adm-avatar-lg" aria-hidden="true">
            {initials(member.fullName)}
          </span>
          <div style={{ minInlineSize: 0 }}>
            <div className="adm-detail-name">{member.fullName}</div>
            <div className="adm-email" dir="ltr">
              {member.email}
            </div>
          </div>
          <span className="adm-detail-role">
            <span className="adm-role-name">{t(`role.${member.role.toLowerCase()}`)}</span>
            <span className="adm-lock">
              <Icon name="lock" size={11} aria-hidden />
              {t('admin.detail.roleReadonly')}
            </span>
          </span>
        </div>

        <div className="adm-detail-facts">
          <div className="adm-fact">
            <div className="adm-fact-label">{t('admin.col.status')}</div>
            <StatusChip tone={STATUS_TONE[member.status] ?? 'neutral'} label={t(`admin.status.${member.status.toLowerCase()}`)} size="sm" />
          </div>
          <div className="adm-fact">
            <div className="adm-fact-label">{t('admin.detail.votingEligible')}</div>
            <div className="adm-fact-value">{member.isVotingEligible ? t('admin.detail.yes') : t('admin.detail.no')}</div>
          </div>
        </div>
      </div>

      <div className="adm-detail-card">
        <div className="adm-detail-section-head">{t('admin.detail.memberships')}</div>
        {member.streams.length === 0 ? (
          <div className="adm-detail-empty">{t('admin.detail.noMemberships')}</div>
        ) : (
          member.streams.map((s) => (
            <div key={s.publicId} className="adm-detail-row">
              <span>{streamName(s, isArabic)}</span>
            </div>
          ))
        )}
      </div>
    </section>
  );
}
