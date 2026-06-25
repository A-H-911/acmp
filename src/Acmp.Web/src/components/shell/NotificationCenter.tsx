/*
 * Notification center shell (docs/14 page 79). The panel + empty/list states
 * live here; the real notification feed is the Notifications module (later
 * phase), so P3 shows the empty inbox. Dismisses on Escape and click-away.
 */
import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { EmptyState } from '../states';

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
  return (
    <div className="notif-panel" ref={ref} role="dialog" aria-label={t('notif.title')}>
      <div className="notif-panel-head">
        <span>{t('notif.title')}</span>
      </div>
      <div style={{ padding: 'var(--sp-4)' }}>
        <EmptyState title={t('notif.emptyTitle')} body={t('notif.emptyBody')} />
      </div>
    </div>
  );
}
