/*
 * Notification center shell (docs/14 page 79). The panel + empty/list states
 * live here; the real notification feed is the Notifications module (later
 * phase), so P3 shows the empty inbox. Dismisses on Escape and click-away.
 */
import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../icons';

export function NotificationCenter({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t } = useTranslation();
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose();
    const onClick = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) onClose();
    };
    document.addEventListener('keydown', onKey);
    document.addEventListener('mousedown', onClick);
    return () => {
      document.removeEventListener('keydown', onKey);
      document.removeEventListener('mousedown', onClick);
    };
  }, [open, onClose]);

  if (!open) return null;
  // Non-modal click-away popover: a labelled region, not a dialog (which would
  // imply focus capture / aria-modal we deliberately don't apply here).
  return (
    <div className="notif-panel" ref={ref} role="region" aria-label={t('notif.title')}>
      <div className="notif-panel-head">
        <span>{t('notif.title')}</span>
      </div>
      <div className="notif-empty">
        <div className="state-icon success"><Icon name="check" size={20} aria-hidden /></div>
        <p className="state-title">{t('notif.emptyTitle')}</p>
        <p className="state-body">{t('notif.emptyBody')}</p>
      </div>
    </div>
  );
}
