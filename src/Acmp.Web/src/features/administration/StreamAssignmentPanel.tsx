import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAssignStreams, useStreams, type Member } from '../../api/members';
import { ApiError } from '../../api/apiClient';
import { Button } from '../../components/ui/Button';
import { Icon } from '../../components/icons';
import { MemberStreamChips } from './MemberStreamChips';

/*
 * BL-024 / ADR-0042 step 3 — assign a member's streams from inside ACMP.
 *
 * ⚠ NO-REFERENCE COMPOSITION (INV-014), for the same reason RoleAssignmentPanel is one. "ACMP
 * Administration.dc.html" §userdetail has NO stream editor: its memberships card is a read-only list
 * of name + role rows, and the directory's add-committee "+" is drawn deliberately inert. That is
 * superseded on the record (ADR-0042 makes stream assignment mandatory), so this panel is composed
 * from the shared design system and the surrounding `adm-detail-*` shell rather than matched to a
 * reference — and it is flagged as such rather than presented as fidelity.
 *
 * ⚠ WHY THIS PANEL EXISTS AT ALL, and it is not a convenience: ADR-0042 makes stream scope
 * FAIL-CLOSED. Once step 7 wires the requirement, a stream-bounded member holding no streams can
 * write NOTHING. Every assignment path therefore has to exist BEFORE that lands, which is why this
 * is step 3 and the wiring is step 7.
 *
 * THE WILDCARD IS OFFERED HERE AND NOWHERE ELSE ON THE TOPIC SIDE. "All streams" says a PERSON is
 * unrestricted (ADR-0042 clause 3); a topic claiming it would be asserting universal scope, which is
 * why useAssignableStreams hides it from the topic picker and this panel uses the unfiltered list.
 * It is rendered DISTINCTLY (DEC-043) — unrestricted access is what an auditor most needs to spot at
 * a glance, and the state most easily granted by accident.
 *
 * The chips live in MemberStreamChips, extracted when InviteUserPanel (step 4) became the second
 * member-side caller. They are NOT StreamPicker: that one hides the wildcard, because it serves the
 * topic side where an 'all streams' claim would be meaningless.
 */
export function StreamAssignmentPanel({ member }: { member: Member }) {
  const { t } = useTranslation();
  const { data: streams, isLoading, isError } = useStreams();
  const assign = useAssignStreams();

  // Seeded from what the member already holds; the panel is keyed on the member by its caller, so
  // opening a different user starts from THAT user's set rather than carrying the previous one.
  const [selected, setSelected] = useState<string[]>(member.streams.map((s) => s.publicId));
  const [error, setError] = useState<string | null>(null);

  const toggle = (publicId: string) =>
    setSelected((prev) =>
      prev.includes(publicId) ? prev.filter((id) => id !== publicId) : [...prev, publicId]);

  const save = async () => {
    setError(null);
    try {
      await assign.mutateAsync({ publicId: member.publicId, streamPublicIds: selected });
    } catch (e) {
      // The server decides who may assign; a refusal is surfaced, never pre-empted by hiding the
      // control — a hidden control is presentation gating and is not what stops a refused change.
      setError(e instanceof ApiError ? e.message : t('common.error'));
    }
  };

  const current = new Set(member.streams.map((s) => s.publicId));
  const isDirty = selected.length !== current.size || selected.some((id) => !current.has(id));

  return (
    <div className="adm-detail-card">
      <div className="adm-detail-section-head">{t('admin.streams.title')}</div>
      <div className="adm-detail-form">
        <p className="field-help">{t('admin.streams.help')}</p>

        {isLoading && <p className="field-help">{t('common.loading')}</p>}
        {(isError || (!isLoading && !streams)) && (
          <p className="field-error" role="alert">
            <Icon name="alertCircle" size={13} aria-hidden />{t('common.error')}
          </p>
        )}

        {streams && (
          <>
            {/* ⚠ The warning is driven by the LIVE selection, not by the saved member, so an
                administrator who clears every stream sees the consequence before saving rather than
                after. Once step 7 lands this state is a refusal, not a label. */}
            {selected.length === 0 && (
              <p className="adm-stream-warning" role="alert">
                <Icon name="alertCircle" size={13} aria-hidden />{t('admin.streams.noneWarning')}
              </p>
            )}

            <MemberStreamChips
              streams={streams}
              selected={selected}
              onToggle={toggle}
              ariaLabel={t('admin.streams.title')}
            />

            {error && (
              <p className="field-error" role="alert">
                <Icon name="alertCircle" size={13} aria-hidden />{error}
              </p>
            )}

            {assign.isSuccess && !isDirty && (
              <p className="adm-role-saved" role="status">
                <Icon name="checkCircle" size={15} aria-hidden />
                {t('admin.streams.saved', { name: member.fullName })}
              </p>
            )}

            {/* Disabled only when nothing changed — mirroring RoleAssignmentPanel. Every refusable
                case stays clickable and answers with its refusal rather than being hidden. */}
            <Button variant="secondary" loading={assign.isPending} disabled={!isDirty} onClick={save}>
              {t('admin.streams.save')}
            </Button>
          </>
        )}
      </div>
    </div>
  );
}
