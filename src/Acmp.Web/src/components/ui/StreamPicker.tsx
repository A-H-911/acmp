/*
 * Affected-streams picker — toggle chips over the committee's seeded stream taxonomy.
 *
 * REPLACES a free-text TokenInput, and the design always said so: SubmitTopic's own header read
 * "free-text token inputs (no committed stream registry in the web yet) rather than the design's
 * fixed stream toggle-chips; revisit when a streams endpoint exists." The endpoint exists now
 * (ADR-0042 step 1 seeded the taxonomy), so this is the deferred revisit, matched to
 * "ACMP Backlog & Topic.dc.html" → submit · scope (INV-014).
 *
 * ⚠ FREE TEXT COULD NEVER HAVE WORKED. The ABAC stream check intersects a topic's streams against
 * Stream.Code — lowercase and code-shaped — so a topic tagged "Smart Cities" would never match the
 * stream "smart-cities" and the check would refuse everything, silently (DEF-057's third finding).
 * The server now rejects anything outside the taxonomy; this control is what makes that reachable
 * rather than a wall the user hits after typing.
 *
 * ⚠ Values are CODES, not display names. The label is localised for the reader; the value posted is
 * always Stream.Code, which is what authorization resolves.
 */
import { useTranslation } from 'react-i18next';
import { streamName, useAssignableStreams } from '../../api/members';
import { Icon } from '../icons';

type Props = {
  id?: string;
  ariaLabel: string;
  ariaInvalid?: boolean;
  describedby?: string;
  /** Selected stream CODES. */
  values: string[];
  onChange: (next: string[]) => void;
};

export function StreamPicker({ id, ariaLabel, ariaInvalid, describedby, values, onChange }: Props) {
  const { t, i18n } = useTranslation();
  const isArabic = i18n.language === 'ar';
  const { data: streams, isLoading, isError } = useAssignableStreams();

  // Loading and failure are surfaced as text rather than an empty chip row: an empty row is
  // indistinguishable from "this committee has no streams", and the user would have no way to tell
  // that a required field is unfillable because a request failed.
  if (isLoading) return <p className="field-help">{t('common.loading')}</p>;
  if (isError || !streams) return <p className="field-error" role="alert">{t('common.error')}</p>;
  if (streams.length === 0) return <p className="field-error" role="alert">{t('common.empty')}</p>;

  const toggle = (code: string) =>
    onChange(values.includes(code) ? values.filter((v) => v !== code) : [...values, code]);

  return (
    // role="group" rather than a listbox: these are independent toggles, not a single-choice widget,
    // and aria-pressed on each button already conveys its state to a screen reader.
    <div className="stream-picker" role="group" id={id} aria-label={ariaLabel}
         aria-invalid={ariaInvalid} aria-describedby={describedby}>
      {streams.map((s) => {
        const on = values.includes(s.code);
        return (
          <button
            key={s.publicId}
            type="button"
            className={`stream-chip ${on ? 'is-on' : ''}`}
            aria-pressed={on}
            onClick={() => toggle(s.code)}
          >
            {on && <Icon name="check" size={13} strokeWidth={2.6} aria-hidden />}
            {streamName(s, isArabic)}
          </button>
        );
      })}
    </div>
  );
}
