import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { useAssignRoles, type Member } from '../../api/members';
import { ApiError } from '../../api/apiClient';
import { Button } from '../../components/ui/Button';
import { Dialog } from '../../components/ui/Dialog';
import { Field } from '../../components/ui/Field';
import { Select } from '../../components/ui/Select';
import { Icon } from '../../components/icons';

/*
 * FR-157 / AC-089 / AC-090 — assign a member's committee role from inside ACMP.
 *
 * ⚠ NO-REFERENCE COMPOSITION (INV-014). "ACMP Administration.dc.html" §(8) has NO role editor: it
 * states the opposite — "the role is assigned there and synced read-only", with a lock icon on the
 * role. That note is superseded, deliberately and on the record (DEC-038 / ADR-0038 / FR-157), so
 * this panel is composed from the shared design system and the surrounding `adm-detail-*` shell
 * rather than matched to a reference, and it is flagged as such rather than presented as fidelity.
 *
 * ONE ROLE, NOT A SET, ON PURPOSE. The API takes a collection, but ACMP caches exactly one
 * CommitteeRole per member and AC-091 forbids reading Keycloak at request time — so the app cannot
 * know a full multi-role set and a multi-select would prefill a partial one and silently drop the
 * roles it could not see. The chosen role is therefore sent as the WHOLE set, which is what the
 * server does with it: the assignment REPLACES the person's committee roles. The panel says so.
 */

// Mirrors the server's PrivilegedRoles list (AssignRolesHandler). The condition is the same one the
// server applies — "the set being SENT contains one of these" — not "this person is gaining
// privilege". Moving an Administrator to Chairman raises nobody's privilege and is still refused
// without the flag, so a "newly granted" reading would send an unconfirmed request and earn a
// refusal the user could do nothing about.
const PRIVILEGED = ['Administrator', 'Chairman'];

// The canonical committee roles (CommitteeRole enum, most-privileged first).
const ROLES = ['Chairman', 'Secretary', 'Member', 'Reviewer', 'Auditor', 'Administrator', 'Submitter', 'Guest'];

export function RoleAssignmentPanel({ member }: { member: Member }) {
  const { t } = useTranslation();
  const [role, setRole] = useState(member.role);
  const [confirming, setConfirming] = useState(false);
  const assign = useAssignRoles();

  const changed = role !== member.role;
  const isPrivileged = PRIVILEGED.includes(role);

  // Picking a different role clears the previous outcome: a stale "saved" or refusal beside a
  // changed selection describes a request that no longer exists.
  function chooseRole(next: string) {
    assign.reset();
    setRole(next);
  }

  function save(confirmedPrivileged: boolean) {
    setConfirming(false);
    assign.mutate({ publicId: member.publicId, roles: [role], confirmedPrivileged });
  }

  function onSave() {
    // Guard 2 is the server's, not this dialog's — the request is refused without the flag. The
    // dialog exists so the flag is never set without a person having actually agreed to it.
    if (isPrivileged) setConfirming(true);
    else save(false);
  }

  return (
    <div className="adm-detail-card adm-card-overflow">
      <div className="adm-detail-section-head">
        <Icon name="shieldUser" size={16} aria-hidden />
        {t('admin.assign.title')}
      </div>

      <div className="adm-detail-form">
        <Field label={t('admin.assign.label')} help={t('admin.assign.note')}>
          {(p) => (
            <Select
              {...p}
              options={ROLES.map((r) => ({ value: r, label: t(`role.${r.toLowerCase()}`) }))}
              value={role}
              onChange={chooseRole}
              ariaLabel={t('admin.assign.label')}
            />
          )}
        </Field>

        {assign.isError && (
          <p className="field-error" role="alert">
            <Icon name="alertCircle" size={13} aria-hidden />
            {refusalMessage(assign.error, t)}
          </p>
        )}

        {assign.isSuccess && (
          <p className="adm-role-saved" role="status">
            <Icon name="checkCircle" size={15} aria-hidden />
            {t('admin.assign.saved', { name: member.fullName })}
          </p>
        )}

        {/* Disabled only when nothing changed — a no-op save is not refused by the server, it
            SUCCEEDS and signs the person out of every session for no reason (AC-090). This is not
            the guards being hidden: every refusable case stays clickable and answers with its
            refusal. */}
        <Button variant="secondary" loading={assign.isPending} disabled={!changed} onClick={onSave}>
          {t('admin.assign.save')}
        </Button>
      </div>

      <Dialog
        open={confirming}
        onClose={() => setConfirming(false)}
        title={t('admin.assign.confirmTitle', { role: t(`role.${role.toLowerCase()}`) })}
        description={t('admin.assign.confirmBody', { name: member.fullName, role: t(`role.${role.toLowerCase()}`) })}
        tone="warn"
        icon={<Icon name="warnTriangle" size={18} aria-hidden />}
        footer={
          <>
            <Button variant="secondary" onClick={() => setConfirming(false)}>
              {t('common.cancel')}
            </Button>
            <Button variant="secondary" className="btn-warn" onClick={() => save(true)}>
              {t('admin.assign.confirmCta')}
            </Button>
          </>
        }
      />
    </div>
  );
}

/**
 * The server sends a GENERIC Problem Details title ("Forbidden", "Conflict") — GlobalExceptionHandler
 * never forwards the exception message — so the reason has to be named here or the user is told only
 * that something was refused.
 *
 * ponytail: status is enough to name the cause FROM THIS SCREEN. /admin is RequireRole
 * ['administrator'] (App.tsx), so the actor is always an Administrator and a 403 cannot be
 * "your role may not assign roles" — it is guard 1, the self-change refusal. And this panel never
 * sends an unconfirmed privileged grant, so the 409 it can provoke is guard 3, the last
 * Administrator. Anything else falls back to the server's own title.
 */
function refusalMessage(error: unknown, t: TFunction): string {
  if (!(error instanceof ApiError)) return t('admin.assign.error');
  if (error.status === 403) return t('admin.assign.errorSelf');
  if (error.status === 409) return t('admin.assign.errorLastAdmin');
  return error.problem?.title ?? t('admin.assign.error');
}
