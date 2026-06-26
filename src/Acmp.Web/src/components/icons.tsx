/*
 * Single stroke-icon component backed by a name→paths map. One component keeps
 * the surface small; add a name here rather than a new file per glyph.
 * Directional glyphs (chevron, arrowEnd) carry className="dir-flip" at the call
 * site so they mirror in RTL.
 */
import type { ReactNode } from 'react';

export type IconName =
  | 'home' | 'backlog' | 'calendar' | 'session' | 'decision' | 'action' | 'adr' | 'risk'
  | 'deps' | 'research' | 'wiki' | 'diagram' | 'reports' | 'audit' | 'admin'
  | 'search' | 'bell' | 'globe' | 'sun' | 'moon' | 'eye' | 'doc' | 'alertCircle' | 'lock'
  | 'plus' | 'chevron' | 'chevronDown' | 'grip' | 'arrowUp' | 'arrowDown' | 'inbox' | 'logout' | 'check' | 'login'
  | 'x' | 'checkCircle' | 'infoCircle' | 'warnTriangle'
  | 'viewTable' | 'viewList' | 'viewKanban' | 'viewTimeline' | 'download' | 'funnel';

const PATHS: Record<IconName, ReactNode> = {
  home: <path d="M3 21h18M5 21V8l7-4 7 4v13M9 21v-6h6v6" />,
  backlog: <><path d="M3 7h18M3 12h18M3 17h18" /></>,
  inbox: <><path d="M22 12h-6l-2 3h-4l-2-3H2" /><path d="M5 5h14l3 7v6a2 2 0 01-2 2H4a2 2 0 01-2-2v-6z" /></>,
  calendar: <><rect x="3" y="4" width="18" height="17" rx="2" /><path d="M3 9h18M8 2v4M16 2v4" /></>,
  session: <path d="M15 10l4.6-3.5a1 1 0 011.4 1v9a1 1 0 01-1.4 1L15 14M3 8a2 2 0 012-2h8a2 2 0 012 2v8a2 2 0 01-2 2H5a2 2 0 01-2-2z" />,
  decision: <><path d="M9 11l3 3L22 4" /><path d="M21 12v7a2 2 0 01-2 2H5a2 2 0 01-2-2V5a2 2 0 012-2h11" /></>,
  action: <><path d="M9 11l3 3 8-8" /><path d="M20 12v6a2 2 0 01-2 2H6a2 2 0 01-2-2V6a2 2 0 012-2h9" /></>,
  adr: <><path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z" /><path d="M14 2v6h6M8 13h8M8 17h8M8 9h2" /></>,
  risk: <><path d="M10.3 3.6L1.8 18a2 2 0 001.7 3h17a2 2 0 001.7-3L14.4 3.6a2 2 0 00-3.4 0z" /><path d="M12 9v4M12 17h.01" /></>,
  deps: <><circle cx="6" cy="6" r="3" /><circle cx="18" cy="18" r="3" /><path d="M9 6h6a3 3 0 013 3v6" /></>,
  research: <><circle cx="11" cy="11" r="7" /><path d="M21 21l-4.3-4.3" /></>,
  wiki: <><path d="M4 19.5A2.5 2.5 0 016.5 17H20" /><path d="M6.5 2H20v20H6.5A2.5 2.5 0 014 19.5v-15A2.5 2.5 0 016.5 2z" /></>,
  diagram: <><circle cx="18" cy="5" r="3" /><circle cx="6" cy="12" r="3" /><circle cx="18" cy="19" r="3" /><path d="M8.6 13.5l6.8 4M15.4 6.5l-6.8 4" /></>,
  reports: <><path d="M3 3v18h18" /><rect x="7" y="11" width="3" height="6" /><rect x="13" y="7" width="3" height="10" /></>,
  audit: <><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" /><path d="M9 12l2 2 4-4" /></>,
  admin: <><circle cx="12" cy="12" r="3" /><path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 11-2.83 2.83l-.06-.06a1.65 1.65 0 00-2.92 1.08V22a2 2 0 01-4 0v-.09A1.65 1.65 0 005 19.4l-.06.06a2 2 0 11-2.83-2.83l.06-.06A1.65 1.65 0 002.6 15H2.5a2 2 0 010-4h.09A1.65 1.65 0 004 8.6l-.06-.06a2 2 0 112.83-2.83l.06.06A1.65 1.65 0 009 5.4V5a2 2 0 014 0v.09a1.65 1.65 0 002.92 1.08l.06-.06a2 2 0 112.83 2.83l-.06.06A1.65 1.65 0 0021.4 11h.1a2 2 0 010 4h-.1z" /></>,
  search: <><circle cx="11" cy="11" r="7" /><path d="M21 21l-4.3-4.3" /></>,
  bell: <path d="M18 8a6 6 0 10-12 0c0 7-3 9-3 9h18s-3-2-3-9M13.7 21a2 2 0 01-3.4 0" />,
  globe: <><circle cx="12" cy="12" r="9" /><path d="M3 12h18M12 3c2.5 2.4 3.8 5.6 3.8 9S14.5 18.6 12 21" /></>,
  sun: <><circle cx="12" cy="12" r="4" /><path d="M12 2v2M12 20v2M2 12h2M20 12h2M5 5l1.5 1.5M17.5 17.5L19 19M19 5l-1.5 1.5M6.5 17.5L5 19" /></>,
  moon: <path d="M21 12.8A9 9 0 1111.2 3a7 7 0 009.8 9.8z" />,
  eye: <><path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7z" /><circle cx="12" cy="12" r="2.5" /></>,
  doc: <><rect x="4" y="4" width="16" height="16" rx="2" /><path d="M9 9h6M9 13h3" /></>,
  alertCircle: <><circle cx="12" cy="12" r="9" /><path d="M12 8v5M12 16h.01" /></>,
  lock: <><rect x="5" y="11" width="14" height="9" rx="2" /><path d="M8 11V8a4 4 0 018 0v3" /></>,
  plus: <path d="M12 5v14M5 12h14" />,
  chevron: <path d="M9 18l6-6-6-6" />,
  chevronDown: <path d="M6 9l6 6 6-6" />,
  grip: <><circle cx="9" cy="6" r="1" /><circle cx="9" cy="12" r="1" /><circle cx="9" cy="18" r="1" /><circle cx="15" cy="6" r="1" /><circle cx="15" cy="12" r="1" /><circle cx="15" cy="18" r="1" /></>,
  arrowUp: <path d="M12 19V5M5 12l7-7 7 7" />,
  arrowDown: <path d="M12 5v14M5 12l7 7 7-7" />,
  logout: <><path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4" /><path d="M16 17l5-5-5-5M21 12H9" /></>,
  check: <path d="M20 6L9 17l-5-5" />,
  login: <path d="M15 3h4a2 2 0 012 2v14a2 2 0 01-2 2h-4M10 17l5-5-5-5M15 12H3" />,
  x: <path d="M18 6L6 18M6 6l12 12" />,
  checkCircle: <><circle cx="12" cy="12" r="9" /><path d="M8 12l3 3 5-6" /></>,
  infoCircle: <><circle cx="12" cy="12" r="9" /><path d="M12 11v5M12 8h.01" /></>,
  warnTriangle: <><path d="M12 4l9 16H3L12 4z" /><path d="M12 11v3M12 17h.01" /></>,
  // Backlog view-switcher + toolbar glyphs (paths lifted from the design file).
  viewList: <path d="M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01" />,
  viewTable: <path d="M3 9h18M3 15h18M9 3v18M5 3h14a2 2 0 012 2v14a2 2 0 01-2 2H5a2 2 0 01-2-2V5a2 2 0 012-2z" />,
  viewKanban: <path d="M4 4h5v16H4zM10 4h5v10h-5zM16 4h4v13h-4z" />,
  viewTimeline: <path d="M3 12h4l3-8 4 16 3-8h4" />,
  download: <path d="M12 3v12M7 10l5 5 5-5M5 21h14" />,
  funnel: <path d="M5 4h14M7 4v6l-3 8h16l-3-8V4" />,
};

interface IconProps {
  name: IconName;
  size?: number;
  className?: string;
  'aria-hidden'?: boolean;
}

export function Icon({ name, size = 18, className, 'aria-hidden': ariaHidden = true }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.7}
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden={ariaHidden}
      focusable={false}
    >
      {PATHS[name]}
    </svg>
  );
}
