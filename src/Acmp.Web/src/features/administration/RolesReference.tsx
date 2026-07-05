/*
 * Administration → Roles (mirrors the "ACMP Administration" `roles` section). Read-only reference:
 * the eight committee roles, their responsibilities, and key capabilities are mirrored from the
 * Keycloak realm roles (ADR-0004) and the docs/domain/permission-role-matrix.md permission matrix — ACMP does not define roles, so
 * this is static canonical content, not a live module. The Usage Map schedules the editable surface
 * to P15; this read-only reference is the honest form available now.
 */
import { useTranslation } from 'react-i18next';
import { Table, type Column } from '../../components/ui/Table';
import { Icon } from '../../components/icons';

// Design order (rolesRaw): chairman, secretary, member, reviewer, auditor, administrator, submitter, guest.
const ROLES = ['chairman', 'secretary', 'member', 'reviewer', 'auditor', 'administrator', 'submitter', 'guest'] as const;

export function RolesReference() {
  const { t } = useTranslation();

  const columns: Column<string>[] = [
    {
      id: 'role',
      header: t('admin.roles.col.role'),
      width: '28%',
      cell: (k) => (
        <span className="adm-role-ref">
          <span className="adm-role-ref-badge" aria-hidden="true">
            <Icon name="shieldUser" size={15} />
          </span>
          <span className="adm-role-name">{t(`role.${k}`)}</span>
        </span>
      ),
    },
    { id: 'who', header: t('admin.roles.col.who'), width: '44%', cell: (k) => <span className="adm-role-who">{t(`admin.roles.${k}.who`)}</span> },
    { id: 'caps', header: t('admin.roles.col.caps'), width: '28%', cell: (k) => <span className="adm-role-caps">{t(`admin.roles.${k}.caps`)}</span> },
  ];

  return (
    <>
      <div className="adm-banner">
        <Icon name="lock" size={17} aria-hidden />
        <div>{t('admin.roles.kcRef')}</div>
      </div>
      <Table caption={t('admin.tabs.roles')} columns={columns} rows={[...ROLES]} getRowKey={(k) => k} />
    </>
  );
}
