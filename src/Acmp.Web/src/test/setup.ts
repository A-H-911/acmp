import '@testing-library/jest-dom/vitest';
import { afterEach } from 'vitest';
import { cleanup } from '@testing-library/react';

// Node 26 ships an experimental global `localStorage` that is undefined without
// a backing file and shadows jsdom's. Install a tiny in-memory Storage shim so
// browser code under test (theme persistence, dev role stub) behaves normally.
function installStorageShim(name: 'localStorage' | 'sessionStorage') {
  const g = globalThis as unknown as Record<string, Storage | undefined>;
  try {
    const existing = g[name];
    if (existing) {
      existing.setItem('__probe', '1');
      existing.removeItem('__probe');
      return; // a working Storage already exists
    }
  } catch {
    // fall through and install the shim
  }
  const store = new Map<string, string>();
  const shim: Storage = {
    get length() { return store.size; },
    clear: () => store.clear(),
    getItem: (k) => (store.has(k) ? store.get(k)! : null),
    key: (i) => [...store.keys()][i] ?? null,
    removeItem: (k) => { store.delete(k); },
    setItem: (k, v) => { store.set(k, String(v)); },
  };
  Object.defineProperty(globalThis, name, { value: shim, configurable: true, writable: true });
}

installStorageShim('localStorage');
installStorageShim('sessionStorage');

// Deterministic locale; clean the DOM between tests.
const { default: i18n } = await import('../i18n');
void i18n.changeLanguage('en');
afterEach(() => cleanup());
