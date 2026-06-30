/*
 * Shared lifecycle/placeholder gate card — the centered empty-card from the design's recording
 * empty-state (ACMP Meetings.dc.html isRecording, recNoTranscript ~L307): 48px rounded icon,
 * 16/700 title, 13 text-2 body. Reused by the conduct gate (meeting not yet running), the Minutes
 * (P7) and Recording (Webex Phase 2) placeholders, and the overview's no-agenda treatment. Styling
 * lives on `.mt-gate` in meetings.css.
 */
import { Icon, type IconName } from '../../components/icons';

export function MeetingGate({ icon, title, body }: { icon: IconName; title: string; body: string }) {
  return (
    <div className="mt-gate" role="status">
      <div className="mt-gate-icon">
        <Icon name={icon} size={22} aria-hidden />
      </div>
      <p className="mt-gate-title">{title}</p>
      <p className="mt-gate-body">{body}</p>
    </div>
  );
}
