import { createContext, useCallback, useContext, useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import type { ReactNode } from 'react';
import { Icon, type IconName } from '../icons';

type ToastTone = 'success' | 'danger' | 'info';

interface ToastData {
  id: number;
  tone: ToastTone;
  title: string;
  description?: string;
}

interface ToastApi {
  /** Queue a toast. Confirms a completed action — never use for errors needing action. */
  toast: (t: { tone: ToastTone; title: string; description?: string }) => void;
}

const ToastContext = createContext<ToastApi | null>(null);

const TONE_ICON: Record<ToastTone, IconName> = {
  success: 'checkCircle',
  danger: 'infoCircle',
  info: 'infoCircle',
};

const AUTO_DISMISS_MS = 5000;

// Module-scoped monotonic id (no Date/Math.random needed; survives re-render).
let nextId = 0;

interface ToastProviderProps {
  children: ReactNode;
  /** Accessible name for the live region (caller-localized). */
  regionLabel?: string;
}

/** App-level toast host: an aria-live region + queue with auto-dismiss (Design System §12). */
export function ToastProvider({ children, regionLabel = 'Notifications' }: ToastProviderProps) {
  const [toasts, setToasts] = useState<ToastData[]>([]);

  const dismiss = useCallback((id: number) => {
    setToasts((cur) => cur.filter((x) => x.id !== id));
  }, []);

  const toast = useCallback<ToastApi['toast']>((t) => {
    setToasts((cur) => [...cur, { ...t, id: nextId++ }]);
  }, []);

  return (
    <ToastContext.Provider value={{ toast }}>
      {children}
      {createPortal(
        <div className="toast-region" role="region" aria-live="polite" aria-label={regionLabel}>
          {toasts.map((t) => (
            <ToastCard key={t.id} data={t} onDismiss={() => dismiss(t.id)} />
          ))}
        </div>,
        document.body,
      )}
    </ToastContext.Provider>
  );
}

export function useToast(): ToastApi {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error('useToast must be used within a ToastProvider');
  return ctx;
}

function ToastCard({ data, onDismiss }: { data: ToastData; onDismiss: () => void }) {
  useEffect(() => {
    const id = setTimeout(onDismiss, AUTO_DISMISS_MS);
    return () => clearTimeout(id);
  }, [onDismiss]);

  return (
    <div className={`toast toast-${data.tone}`}>
      <Icon name={TONE_ICON[data.tone]} size={18} className="toast-icon" aria-hidden />
      <div className="toast-text">
        <div className="toast-title">{data.title}</div>
        {data.description && <div className="toast-desc">{data.description}</div>}
      </div>
      <button type="button" className="toast-close" onClick={onDismiss} aria-label="Dismiss">
        <Icon name="x" size={15} aria-hidden />
      </button>
    </div>
  );
}
