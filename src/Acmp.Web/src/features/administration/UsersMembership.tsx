/*
 * Administration → Users & Membership (mirrors the "ACMP Administration" Claude Design file, that
 * screen only). Visuals per the design; behavior per the package: the directory is read-only here
 * because identities and roles come from Keycloak (ADR-0004) — ACMP creates no accounts and edits
 * no roles. Wired to GET /api/members (AC-059). Roles, voting eligibility, and status are surfaced;
 * editing voting eligibility / topic assignments arrive with the Voting (P9) and Topics (P5) phases.
 */
import { useTranslation } from 'react-i18next';
import { useMembers, type Member } from '../../api/members';
import { StatusChip, type StatusTone } from '../../components/ui/StatusChip';
import { LoadingState, ErrorState, EmptyState } from '../../components/states';
import '../../styles/administration.css';

const STATUS_TONE: Record<string, StatusTone> = {
  Active: 'success',
  Invited: 'info',
  Disabled: 'neutral',
};

function initials(name: string): string {
  const parts = name.trim().split(/\s+/);
  return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || '?';
}

function InfoIcon() {
  return (
    <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" aria-hidden="true">
      <circle cx="12" cy="12" r="9" />
      <path d="M12 11v5M12 8h.01" />
    </svg>
  );
}

function LockIcon() {
  return (
    <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.9" strokeLinecap="round" aria-hidden="true">
      <rect x="5" y="11" width="14" height="9" rx="2" />
      <path d="M8 11V8a4 4 0 018 0v3" />
    </svg>
  );
}

function CheckIcon() {
  return (
    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="var(--text-3)" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M9 11l3 3L21 5M21 12v6a2 2 0 01-2 2H5a2 2 0 01-2-2V6a2 2 0 012-2h11" />
    </svg>
  );
}

const TABS = ['users', 'templates', 'health', 'notifications'] as const;

export function UsersMembership() {
  const { t, i18n } = useTranslation();
  const { data, isLoading, isError, refetch } = useMembers();
  const isArabic = i18n.language === 'ar';

  return (
    <section className="page">
      <div className="adm-head">
        <div>
          <h1 className="page-title">{t('admin.title')}</h1>
          <div className="adm-head-sub">{t('admin.subtitle')}</div>
        </div>
        <button type="button" className="adm-provision">{t('admin.provision')}</button>
      </div>

      <div className="adm-tabs" role="tablist" aria-label={t('admin.title')}>
        {TABS.map((tab) => (
          <button
            key={tab}
            type="button"
            role="tab"
            className="adm-tab"
            aria-current={tab === 'users' ? 'page' : undefined}
            aria-selected={tab === 'users'}
            disabled={tab !== 'users'}
          >
            {t(`admin.tabs.${tab}`)}
          </button>
        ))}
      </div>

      {isLoading ? (
        <LoadingState />
      ) : isError ? (
        <ErrorState onRetry={() => refetch()} />
      ) : !data || data.length === 0 ? (
        <EmptyState title={t('admin.emptyTitle')} body={t('admin.emptyBody')} />
      ) : (
        <Directory members={data} isArabic={isArabic} />
      )}
    </section>
  );
}

function Directory({ members, isArabic }: { members: Member[]; isArabic: boolean }) {
  const { t } = useTranslation();
  return (
    <>
      <div className="adm-banner">
        <InfoIcon />
        <div>
          <b>{t('admin.kc.title')}</b> — {t('admin.kc.note')}
        </div>
      </div>

      <div className="adm-filters">
        {(['role', 'status', 'membership'] as const).map((f) => (
          <button key={f} type="button" className="adm-filter">
            {t(`admin.filter.${f}`)}
            <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
              <path d="M6 9l6 6 6-6" />
            </svg>
          </button>
        ))}
        <span className="adm-count">{t('admin.showing', { count: members.length })}</span>
      </div>

      <div className="adm-table" role="table" aria-label={t('admin.tabs.users')}>
        <div className="adm-grid adm-hrow" role="row">
          <span className="adm-hcell" role="columnheader">{t('admin.col.user')}</span>
          <span className="adm-hcell" role="columnheader">{t('admin.col.role')}</span>
          <span className="adm-hcell" role="columnheader">{t('admin.col.membership')}</span>
          <span className="adm-hcell" role="columnheader">{t('admin.col.assignments')}</span>
          <span className="adm-hcell" role="columnheader">{t('admin.col.status')}</span>
        </div>

        {members.map((m) => (
          <div className="adm-grid adm-row" role="row" key={m.publicId}>
            <span className="adm-cell adm-user" role="cell">
              <span className="adm-avatar" aria-hidden="true">{initials(m.fullName)}</span>
              <span style={{ minInlineSize: 0 }}>
                <span className="adm-name">{m.fullName}</span>
                <span className="adm-email" dir="ltr">{m.email}</span>
              </span>
            </span>

            <span className="adm-cell" role="cell">
              <span className="adm-role">
                <span className="adm-role-name">{t(`role.${m.role.toLowerCase()}`)}</span>
                <span className="adm-lock"><LockIcon />{t('admin.fromKeycloak')}</span>
              </span>
            </span>

            <span className="adm-cell" role="cell">
              <span className="adm-membership">
                <span className="adm-chips">
                  {m.streams.length === 0 ? (
                    <span className="adm-chip observer">{t('admin.observer')}</span>
                  ) : (
                    m.streams.map((s) => (
                      <span className="adm-chip" key={s.publicId}>{isArabic ? s.nameAr : s.nameEn}</span>
                    ))
                  )}
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
                  {t('admin.votingEligible')}
                </span>
              </span>
            </span>

            <span className="adm-cell" role="cell">
              <span className="adm-assign" title={t('admin.assignmentsHint')}>
                <CheckIcon />—
              </span>
            </span>

            <span className="adm-cell" role="cell">
              <StatusChip tone={STATUS_TONE[m.status] ?? 'neutral'} label={t(`admin.status.${m.status.toLowerCase()}`)} />
            </span>
          </div>
        ))}
      </div>
    </>
  );
}
