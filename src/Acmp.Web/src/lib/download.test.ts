import { describe, it, expect, vi, afterEach } from 'vitest';
import { startDownload } from './download';

// AC-164 / AC-165: a download is a click on a plain link (no new tab, no popup to block), and only a web link.
describe('startDownload', () => {
  afterEach(() => vi.restoreAllMocks());

  it('clicks a temporary link with no target, then removes it', () => {
    const clicked: HTMLAnchorElement[] = [];
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) { clicked.push(this); });

    startDownload('https://s3.example/bucket/key?X-Amz-Signature=abc');

    expect(clicked).toHaveLength(1);
    expect(clicked[0].href).toBe('https://s3.example/bucket/key?X-Amz-Signature=abc');
    expect(clicked[0].target).toBe('');
    expect(clicked[0].hasAttribute('download')).toBe(true);
    expect(clicked[0].isConnected).toBe(false);
  });

  it('refuses a javascript: or data: link', () => {
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
    expect(() => startDownload('javascript:alert(1)')).toThrow();
    expect(() => startDownload('data:text/html,x')).toThrow();
    expect(click).not.toHaveBeenCalled();
  });
});
