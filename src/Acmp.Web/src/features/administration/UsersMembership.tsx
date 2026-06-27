/*
 * Administration → Users & Membership (mirrors the "ACMP Administration" design file, that
 * screen only). Composes the shared component library (Tabs, Table, Button, Tag, StatusChip,
 * Icon, states) — only the domain-specific directory cells carry local CSS. Behavior per the
 * package: the directory is read-only because identities and roles come from Keycloak (ADR-0004);
 * editing voting eligibility / topic assignments arrive with Voting (P9) and Topics (P5).
 * Wired to GET /api/members (AC-059).
 */
import { useTranslation } from 'react-i18next';
import { useMembers, type Member } from '../../api/members';
import { StatusChip, type StatusTone } from '../../components/ui/StatusChip';
import { LoadingState, ErrorState, EmptyState } from '../../components/states';
import { Button } from '../../components/ui/Button';
import { Tabs } from '../../components/ui/Tabs';
import { Table, type Column } from '../../components/ui/Table';
import { Tag } from '../../components/ui/Chip';
import { Icon } from '../../components/icons';
import '../../styles/administration.css';

const STATUS_TONE: Record<string, StatusTone> = {
  Active: 'success',
  Invited: 'info',
  Disabled: 'neutral',
};

const TAB_IDS = ['users', 'templates', 'health', 'notifications'] as const;

function initials(name: string): string {
  const parts = name.trim().split(/\s+/);
  return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || '?';
}

export function UsersMembership() {
  const { t, i18n } = useTranslation();
  const { data, isLoading, isError, refetch } = useMembers();
  const isArabic = i18n.language === 'ar';

  // Only Users & Membership is implemented this phase; the rest are disabled placeholders.
  const tabs = TAB_IDS.map((id) => ({ id, label: t(`admin.tabs.${id}`), disabled: id !== 'users' }));

  return (
    <section className="page">
      <div className="adm-head">
        <div>
          <h1 className="page-title">{t('admin.title')}</h1>
          <div className="adm-head-sub">{t('admin.subtitle')}</div>
        </div>
        <Button>{t('admin.provision')}</Button>
      </div>

      <Tabs items={tabs} value="users" onValueChange={() => {}} ariaLabel={t('admin.title')} />

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
      width: '17%',
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
      width: '26%',
      cell: (m) => (
        <span className="adm-membership">
          <span className="adm-chips">
            {m.streams.length === 0 ? (
              <Tag className="adm-observer">{t('admin.observer')}</Tag>
            ) : (
              m.streams.map((s) => <Tag key={s.publicId}>{isArabic ? s.nameAr : s.nameEn}</Tag>)
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
      ),
    },
    {
      id: 'assignments',
      header: t('admin.col.assignments'),
      width: '12%',
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
      cell: (m) => <StatusChip tone={STATUS_TONE[m.status] ?? 'neutral'} label={t(`admin.status.${m.status.toLowerCase()}`)} size="sm" />,
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
