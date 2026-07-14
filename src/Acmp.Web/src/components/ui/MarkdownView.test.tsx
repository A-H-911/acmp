import { describe, it, expect } from 'vitest';
import { render } from '@testing-library/react';
import { renderMarkdown, MarkdownView } from './MarkdownView';

describe('MarkdownView / renderMarkdown', () => {
  it('renders basic markdown to the expected tags', () => {
    const html = renderMarkdown('# Title\n\nSome **bold** and *italic* text.\n\n- one\n- two');
    expect(html).toContain('<h1>Title</h1>');
    expect(html).toContain('<strong>bold</strong>');
    expect(html).toContain('<em>italic</em>');
    expect(html).toContain('<li>one</li>');
  });

  it('renders blockquotes, code and links', () => {
    const html = renderMarkdown('> quote\n\n`inline` and [a link](https://example.com)');
    expect(html).toContain('<blockquote>');
    expect(html).toContain('<code>inline</code>');
    expect(html).toContain('href="https://example.com"');
  });

  it('turns [[KEY]] into an xref chip', () => {
    const html = renderMarkdown('See [[TOP-2026-014]] for context.');
    expect(html).toContain('<span class="xref">TOP-2026-014</span>');
  });

  it('hardens rendered links with rel/target', () => {
    const html = renderMarkdown('[x](https://example.com)');
    expect(html).toContain('rel="noopener noreferrer"');
    expect(html).toContain('target="_blank"');
  });

  // The security contract: sanitize → inject → assert no script/handlers survive (cure53 pattern).
  it('strips script tags, inline event handlers and javascript: urls', () => {
    const payload = '<p onclick="alert(1)">hi</p><script>alert(2)</script>'
      + '<img src=x onerror="alert(3)"><a href="javascript:alert(4)">x</a>'
      + '<style>body{display:none}</style><iframe src="evil"></iframe>';
    const clean = renderMarkdown(payload);

    const container = document.createElement('div');
    container.innerHTML = clean;

    expect(container.querySelector('script')).toBeNull();
    expect(container.querySelector('[onclick],[onerror],[onload]')).toBeNull();
    expect(container.querySelector('style')).toBeNull();
    expect(container.querySelector('iframe')).toBeNull();
    expect(container.querySelector('img')).toBeNull(); // <img> is not in the allowlist
    expect(clean.toLowerCase()).not.toContain('javascript:');
  });

  it('tolerates an empty/undefined body', () => {
    expect(renderMarkdown('')).toBe('');
    expect(renderMarkdown(undefined as unknown as string)).toBe('');
  });

  it('renders into a themed container element', () => {
    const { container } = render(<MarkdownView markdown="# Hi" className="wiki-artbody" />);
    const body = container.querySelector('.markdown-body');
    expect(body).not.toBeNull();
    expect(body?.classList.contains('wiki-artbody')).toBe(true);
    expect(body?.innerHTML).toContain('<h1>Hi</h1>');
  });
});
