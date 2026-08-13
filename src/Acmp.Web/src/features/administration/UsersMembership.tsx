/*
 * Administration → Users & Membership body (mirrors the "ACMP Administration" `users` + `userdetail`
 * sections). The 7-tab strip and page header live in AdministrationPage; this file owns the directory
 * and the read-only user-detail drill-down.
 *
 * Behavior:
 *  - The DIRECTORY is read-only (GET /api/members). Membership editing affordances (committee ×
 *    remove, dashed + add, voting-eligibility switch) are rendered to match the design but are
 *    INERT/disabled. ⚠ STREAM ASSIGNMENT IS NO LONGER INERT — it landed with ADR-0042 step 3, but in
 *    the USER DETAIL (StreamAssignmentPanel), not on these directory affordances, which stay inert.
 *    Voting eligibility still lands with Voting (P9).
 *  - The row's view button opens an in-place user detail (state lifted to the container). That
 *    detail is NO LONGER read-only: ADR-0038 supersedes ADR-0015 §Q3's "no Keycloak Admin API in
 *    v1" clause (SC-004), so the detail hosts the invite section (FR-156) and the role assignment
 *    (FR-157). Identity still lives in Keycloak — both write THROUGH to it.
 * Wired to GET /api/members (AC-059).
 */
import { useTranslation } from 'react-i18next';
import { useMembers, type Member } from '../../api/members';
import { InviteUserPanel } from './InviteUserPanel';
import { RoleAssignmentPanel } from './RoleAssignmentPanel';
import { StreamAssignmentPanel } from './StreamAssignmentPanel';
import { StatusChip, type StatusTone } from '../../components/ui/StatusChip';
import { LoadingState, ErrorState, EmptyState } from '../../components/states';
import { Table, type Column } from '../../components/ui/Table';
import { Icon } from '../../components/icons';

const STATUS_TONE: Record<string, StatusTone> = {
  Active: 'success',
  Invited: 'info',
  Disabled: 'neutral',
};

function initials(name: string): string {
  const parts = name.trim().split(/\s+/);
  return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || '?';
}

function streamName(s: { nameEn: string; nameAr: string }, isArabic: boolean): string {
  return isArabic ? s.nameAr : s.nameEn;
}

export function UsersDirectory({ onView }: { onView: (m: Member) => void }) {
  const { t, i18n } = useTranslation();
  const { data, isLoading, isError, refetch } = useMembers();
  const isArabic = i18n.language === 'ar';

  if (isLoading) return <LoadingState />;
  if (isError) return <ErrorState onRetry={() => refetch()} />;
  if (!data || data.length === 0) return <EmptyState title={t('admin.emptyTitle')} body={t('admin.emptyBody')} icon="usersGroup" />;

  return <Directory members={data} isArabic={isArabic} onView={onView} />;
}

function Directory({ members, isArabic, onView }: { members: Member[]; isArabic: boolean; onView: (m: Member) => void }) {
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
            {/* Add-committee: editing lands with stream assignment (BL-024) — inert this phase.
                It was already `disabled` and already dimmed by .adm-add:disabled, so it was never a
                live-looking control. What it did not do was say WHY, so a reader saw a plus sign and
                reasonably expected it to work. Every other inert affordance here carries an
                explanatory `title` (topics.comingSoon, meetings.comingSoon); this one now does too. */}
            <button
              type="button"
              className="adm-add"
              aria-label={t('admin.addCommittee')}
              title={t('admin.addCommitteeSoon')}
              disabled
            >
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
      // No assignment count on the member API yet (topic/action modules → later phase) — honest dash + tooltip.
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
      {/* This banner used to read "Roles are read-only — ACMP does not create accounts or edit
          roles". Half of that is now false: ADR-0038 supersedes ADR-0015 §Q3 (SC-004) and the user
          detail both invites and assigns. The still-true half — no self-registration, Keycloak as
          the source of truth — is kept, because dropping it would lose a real constraint. */}
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
 * User detail (the design's user-detail panel). Renders only data the member API returns — Keycloak
 * ID / last sign-in / provisioned date are omitted until the directory exposes them — plus the two
 * ADR-0038 write affordances at the foot: role assignment (FR-157) and invite (FR-156).
 */
export function UserDetail({ member, isArabic, onBack }: { member: Member; isArabic: boolean; onBack: () => void }) {
  const { t } = useTranslation();
  // The container holds the clicked member as a SNAPSHOT, so after a role change the head would keep
  // showing the old role and the successful change would read as having failed. This re-reads the
  // same ['members'] cache entry the directory already populated — free, and live after the
  // assignment invalidates it.
  const { data } = useMembers();
  const current = data?.find((m) => m.publicId === member.publicId) ?? member;
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
            {initials(current.fullName)}
          </span>
          <div style={{ minInlineSize: 0 }}>
            <div className="adm-detail-name">{current.fullName}</div>
            <div className="adm-email" dir="ltr">
              {current.email}
            </div>
          </div>
          <span className="adm-detail-role">
            <span className="adm-role-name">{t(`role.${current.role.toLowerCase()}`)}</span>
            {/* Was a padlock reading "Role is read-only — managed in Keycloak". That is no longer
                true on THIS screen (FR-157 assigns it below), and a lock beside an editor is worse
                than no note at all. Keycloak is still where the role LIVES, which is what the
                replacement says. */}
            <span className="adm-lock">
              <Icon name="shieldUser" size={11} aria-hidden />
              {t('admin.detail.roleSource')}
            </span>
          </span>
        </div>

        <div className="adm-detail-facts">
          <div className="adm-fact">
            <div className="adm-fact-label">{t('admin.col.status')}</div>
            <StatusChip tone={STATUS_TONE[current.status] ?? 'neutral'} label={t(`admin.status.${current.status.toLowerCase()}`)} size="sm" />
          </div>
          <div className="adm-fact">
            <div className="adm-fact-label">{t('admin.detail.votingEligible')}</div>
            <div className="adm-fact-value">{current.isVotingEligible ? t('admin.detail.yes') : t('admin.detail.no')}</div>
          </div>
        </div>
      </div>

      <div className="adm-detail-card">
        <div className="adm-detail-section-head">{t('admin.detail.memberships')}</div>
        {current.streams.length === 0 ? (
          <div className="adm-detail-empty">{t('admin.detail.noMemberships')}</div>
        ) : (
          current.streams.map((s) => (
            <div key={s.publicId} className="adm-detail-row">
              <span>{streamName(s, isArabic)}</span>
            </div>
          ))
        )}
      </div>

      {/* FR-157 — role assignment. Keyed on the member so re-opening a different user starts from
          THAT user's role rather than carrying the previous panel's selection. */}
      <RoleAssignmentPanel key={current.publicId} member={current} />

      {/* BL-024 / ADR-0042 step 3 — stream assignment. Keyed for the same reason as the role panel.
          It sits AFTER the role panel deliberately: which streams someone needs only makes sense
          once you know their role, and CommitteeWide roles bypass stream scope entirely. */}
      <StreamAssignmentPanel key={`streams-${current.publicId}`} member={current} />

      {/* §(8) places the invite section at the foot of the user detail view (FR-156). The server
          decides who may actually invite — Administrator or Secretary — so this is not gated here;
          navModel.ts is explicit that the SPA does presentation gating only. */}
      <InviteUserPanel />
    </section>
  );
}
