import { useTranslation } from 'react-i18next';
import { streamName, type StreamRef } from '../../api/members';
import { Icon } from '../../components/icons';

/*
 * The MEMBER-side stream chips, shared by StreamAssignmentPanel (ADR-0043 step 3) and
 * InviteUserPanel (step 4). Extracted when the second caller arrived, which is the same trigger that
 * produced TokenInput and StreamPicker rather than a speculative abstraction.
 *
 * ⚠ MEMBER-SIDE IS NOT THE SAME LIST AS TOPIC-SIDE, which is why this is not StreamPicker. The topic
 * picker draws from useAssignableStreams, which EXCLUDES the wildcard: "All streams" says a PERSON is
 * unrestricted, so a topic claiming it would assert universal scope (ADR-0043 clause 4). Here the
 * wildcard is legitimate and offered — and rendered distinctly (DEC-043) so unrestricted access is
 * spottable at a glance rather than hidden among the delivery streams.
 *
 * ⚠ VALUES ARE PublicIds, not codes. The assignment endpoints take stream PublicIds; only the TOPIC
 * side works in codes, because that is what the ABAC intersect resolves.
 */
export function MemberStreamChips({
  streams, selected, onToggle, ariaLabel,
}: {
  streams: StreamRef[];
  /** Selected stream PublicIds. */
  selected: string[];
  onToggle: (publicId: string) => void;
  ariaLabel: string;
}) {
  const { i18n } = useTranslation();
  const isArabic = i18n.language === 'ar';

  return (
    <div className="stream-picker" role="group" aria-label={ariaLabel}>
      {streams.map((s) => {
        const on = selected.includes(s.publicId);
        return (
          <button
            key={s.publicId}
            type="button"
            className={`stream-chip ${on ? 'is-on' : ''} ${s.isWildcard ? 'is-wildcard' : ''}`}
            aria-pressed={on}
            onClick={() => onToggle(s.publicId)}
          >
            {on && <Icon name="check" size={13} strokeWidth={2.6} aria-hidden />}
            {streamName(s, isArabic)}
          </button>
        );
      })}
    </div>
  );
}
