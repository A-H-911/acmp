import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider, createBrowserRouter } from 'react-router-dom';
import { QueryClientProvider } from '@tanstack/react-query';

// Self-hosted fonts (bundled by Vite, not a CDN) — works air-gapped (CON-001).
import '@fontsource/ibm-plex-sans/400.css';
import '@fontsource/ibm-plex-sans/500.css';
import '@fontsource/ibm-plex-sans/600.css';
import '@fontsource/ibm-plex-sans/700.css';
import '@fontsource/ibm-plex-sans-arabic/400.css';
import '@fontsource/ibm-plex-sans-arabic/500.css';
import '@fontsource/ibm-plex-sans-arabic/600.css';
import '@fontsource/ibm-plex-sans-arabic/700.css';
import '@fontsource/ibm-plex-serif/400.css';
import '@fontsource/ibm-plex-serif/600.css';
import '@fontsource/ibm-plex-mono/400.css';

import './i18n';
import './styles/tokens.css';
import './styles/global.css';
import './styles/components.css';
import './styles/forms.css';
import './styles/controls.css';
import './styles/overlays.css';
import { appRoutes } from './App.tsx';
import { AuthProvider } from './auth/AuthProvider';
import { MobileNotice } from './components/shell/MobileNotice';
import { queryClient } from './api/queryClient';

const router = createBrowserRouter(appRoutes);

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        {/* NFR-063 / DW-061 — mounted here rather than in AppShell because the shell wraps only
            authenticated routes, and /login is the first thing a phone user reaches. CSS-only
            visibility: `display: none` above 767.98px, so it costs the desktop path nothing. */}
        <MobileNotice />
        <RouterProvider router={router} />
      </AuthProvider>
    </QueryClientProvider>
  </StrictMode>,
);
