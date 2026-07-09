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
  | 'plus' | 'minus' | 'chevron' | 'chevronDown' | 'chevronUp' | 'grip' | 'arrowUp' | 'arrowDown' | 'inbox' | 'logout' | 'check' | 'login'
  | 'x' | 'checkCircle' | 'infoCircle' | 'warnTriangle' | 'clock' | 'user' | 'send'
  | 'viewTable' | 'viewList' | 'viewKanban' | 'viewTimeline' | 'download' | 'upload' | 'funnel'
  | 'usersGroup' | 'template' | 'activity' | 'stream' | 'shieldUser' | 'cog'
  | 'server' | 'database' | 'box' | 'mail' | 'video' | 'refresh'
  | 'arrowRight' | 'arrowLeft' | 'clipboardCheck' | 'pause' | 'ban' | 'arrowUpRight'
  | 'shieldPlus' | 'filterLines' | 'checklist' | 'trash';

const PATHS: Record<IconName, ReactNode> = {
  home: <path d="M3 21h18M5 21V8l7-4 7 4v13M9 21v-6h6v6" />,
  backlog: <><path d="M3 7h18M3 12h18M3 17h18" /></>,
  inbox: <><path d="M22 12h-6l-2 3h-4l-2-3H2" /><path d="M5 5h14l3 7v6a2 2 0 01-2 2H4a2 2 0 01-2-2v-6z" /></>,
  calendar: <><rect x="3" y="4" width="18" height="17" rx="2" /><path d="M3 9h18M8 2v4M16 2v4" /></>,
  session: <path d="M15 10l4.6-3.5a1 1 0 011.4 1v9a1 1 0 01-1.4 1L15 14M3 8a2 2 0 012-2h8a2 2 0 012 2v8a2 2 0 01-2 2H5a2 2 0 01-2-2z" />,
  decision: <><path d="M9 11l3 3L22 4" /><path d="M21 12v7a2 2 0 01-2 2H5a2 2 0 01-2-2V5a2 2 0 012-2h11" /></>,
  action: <><path d="M9 11l3 3 8-8" /><path d="M20 12v6a2 2 0 01-2 2H6a2 2 0 01-2-2V6a2 2 0 012-2h9" /></>,
  // ADR mark = a book/record (matches the design's ADR glyph in Lists & Registers + Decision detail).
  adr: <path d="M6 4h13v16H6a2 2 0 00-2 2V6a2 2 0 012-2zM9 8h7M9 12h7" />,
  trash: <path d="M4 7h16M9 7V4a1 1 0 011-1h4a1 1 0 011 1v3M18 7l-1 13a1 1 0 01-1 1H8a1 1 0 01-1-1L6 7M10 11v6M14 11v6" />,
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
  minus: <path d="M5 12h14" />,
  chevron: <path d="M9 18l6-6-6-6" />,
  chevronDown: <path d="M6 9l6 6 6-6" />,
  chevronUp: <path d="M5 15l7-7 7 7" />,
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
  // Agenda-builder glyphs (paths lifted from the local .dc.html for fidelity).
  clock: <><circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" /></>,
  user: <path d="M20 21v-2a4 4 0 00-4-4H8a4 4 0 00-4 4v2M12 11a4 4 0 100-8 4 4 0 000 8z" />,
  send: <path d="M22 2L11 13M22 2l-7 20-4-9-9-4 20-7z" />,
  // Backlog view-switcher + toolbar glyphs (paths lifted from the design file).
  viewList: <path d="M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01" />,
  viewTable: <path d="M3 9h18M3 15h18M9 3v18M5 3h14a2 2 0 012 2v14a2 2 0 01-2 2H5a2 2 0 01-2-2V5a2 2 0 012-2z" />,
  viewKanban: <path d="M4 4h5v16H4zM10 4h5v10h-5zM16 4h4v13h-4z" />,
  viewTimeline: <path d="M3 12h4l3-8 4 16 3-8h4" />,
  download: <path d="M12 3v12M7 10l5 5 5-5M5 21h14" />,
  upload: <path d="M12 16V4M7 9l5-5 5 5M5 20h14" />,
  funnel: <path d="M5 4h14M7 4v6l-3 8h16l-3-8V4" />,
  // Administration sub-tab glyphs (paths lifted from ACMP Administration.dc.html for fidelity).
  usersGroup: <path d="M17 21v-2a4 4 0 00-4-4H7a4 4 0 00-4 4v2M11 11a4 4 0 100-8 4 4 0 000 8zM21 21v-2a4 4 0 00-3-3.9M16 3.1a4 4 0 010 7.8" />,
  template: <path d="M14 3v5h5M14 3H7a2 2 0 00-2 2v14a2 2 0 002 2h10a2 2 0 002-2V8l-5-5zM9 13h6M9 17h4" />,
  activity: <path d="M22 12h-4l-3 9L9 3l-3 9H2" />,
  stream: <path d="M3 12h4l3 8 4-16 3 8h4" />,
  shieldUser: <path d="M12 2l8 4.5v9L12 22l-8-4.5v-9L12 2zM12 7a3 3 0 100 6 3 3 0 000-6zM7.5 18a4.5 4.5 0 019 0" />,
  // Architecture-invariant mark = shield + plus (the design's invariant glyph in the register tab + create form).
  shieldPlus: <path d="M12 3l8 4.5v9L12 21l-8-4.5v-9L12 3zM12 7v6M9 9.5h6" />,
  // Filtered-count glyph (narrowing lines) next to the register "Showing N" line — the design's funnel mark.
  filterLines: <path d="M3 4h18M6 8h12M9 12h6M11 16h2" />,
  // Generic empty-state glyph (checklist-check) shared by every register's empty state in the design.
  checklist: <path d="M9 11l3 3 8-8M3 12h.01M3 7h.01M3 17h.01" />,
  cog: <path d="M12 8a4 4 0 100 8 4 4 0 000-8zM12 2v3M12 19v3M2 12h3M19 12h3M5 5l2 2M17 17l2 2M19 5l-2 2M7 17l-2 2" />,
  // System Health + Notification channel glyphs (paths lifted from ACMP Administration.dc.html).
  server: <path d="M3 5h18v6H3zM3 13h18v6H3zM7 8h.01M7 16h.01" />,
  database: <path d="M12 3c4.4 0 8 1.3 8 3s-3.6 3-8 3-8-1.3-8-3 3.6-3 8-3zM4 6v12c0 1.7 3.6 3 8 3s8-1.3 8-3V6" />,
  box: <path d="M21 8l-9-5-9 5 9 5 9-5zM3 8v8l9 5 9-5V8" />,
  mail: <path d="M3 7a2 2 0 012-2h14a2 2 0 012 2v10a2 2 0 01-2 2H5a2 2 0 01-2-2zM3 7l9 6 9-6" />,
  video: <path d="M15 10l5-3v10l-5-3M3 7a2 2 0 012-2h8a2 2 0 012 2v10a2 2 0 01-2 2H5a2 2 0 01-2-2z" />,
  refresh: <path d="M3 12a9 9 0 109-9 9 9 0 00-7 3.3M3 4v4h4" />,
  // Directional — carries className="dir-flip" at the call site so it mirrors in RTL.
  arrowRight: <path d="M5 12h14M13 6l6 6-6 6" />,
  arrowLeft: <path d="M19 12H5M11 6l-6 6 6 6" />,
  ban: <><circle cx="12" cy="12" r="9" /><path d="M5.6 5.6l12.8 12.8" /></>,
  arrowUpRight: <path d="M7 17L17 7M9 7h8v8" />,
  // Ballot / clipboard-check — cast & confirm-vote actions (Decision, Voting & ADR.dc.html).
  clipboardCheck: <><path d="M9 3h6a1 1 0 011 1v1a1 1 0 01-1 1H9a1 1 0 01-1-1V4a1 1 0 011-1z" /><path d="M8 5H6a2 2 0 00-2 2v12a2 2 0 002 2h12a2 2 0 002-2V7a2 2 0 00-2-2h-2" /><path d="M9 14l2 2 4-4" /></>,
  // Meeting workspace Pause control.
  pause: <><rect x="6" y="5" width="4" height="14" rx="1" /><rect x="14" y="5" width="4" height="14" rx="1" /></>,
};

interface IconProps {
  name: IconName;
  size?: number;
  // Per-glyph stroke weight: the design uses ~1.6 for nav glyphs and 2 for chevrons/carets.
  // Default stays 1.7 (the settled compromise); override at the call site to match the reference.
  strokeWidth?: number;
  className?: string;
  'aria-hidden'?: boolean;
}

export function Icon({ name, size = 18, strokeWidth = 1.7, className, 'aria-hidden': ariaHidden = true }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={strokeWidth}
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
