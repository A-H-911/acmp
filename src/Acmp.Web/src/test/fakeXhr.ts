import { vi } from 'vitest';

/*
 * A stand-in XMLHttpRequest for apiUpload (DEF-171): records what was sent, lets a test drive upload
 * progress, and finishes with a chosen status and body. Install with installFakeXhr(); the instance a call
 * created is FakeXhr.last.
 */
export class FakeXhr {
  static last: FakeXhr | undefined;
  method = '';
  url = '';
  headers: Record<string, string> = {};
  body: unknown;
  status = 0;
  responseText = '';
  responseHeaders: Record<string, string> = {};
  upload: { onprogress: ((e: { lengthComputable: boolean; loaded: number; total: number }) => void) | null } = { onprogress: null };
  onload: (() => void) | null = null;
  onerror: (() => void) | null = null;
  onabort: (() => void) | null = null;

  constructor() {
    FakeXhr.last = this;
  }
  open(method: string, url: string) {
    this.method = method;
    this.url = url;
  }
  setRequestHeader(name: string, value: string) {
    this.headers[name] = value;
  }
  getResponseHeader(name: string) {
    return this.responseHeaders[name] ?? null;
  }
  send(body: unknown) {
    this.body = body;
  }

  progress(loaded: number, total: number) {
    this.upload.onprogress?.({ lengthComputable: true, loaded, total });
  }
  respond(status: number, body?: unknown, headers: Record<string, string> = {}) {
    this.status = status;
    this.responseText = body === undefined ? '' : JSON.stringify(body);
    this.responseHeaders = headers;
    this.onload?.();
  }
  fail() {
    this.onerror?.();
  }
}

export function installFakeXhr(): void {
  FakeXhr.last = undefined;
  vi.stubGlobal('XMLHttpRequest', FakeXhr);
}
