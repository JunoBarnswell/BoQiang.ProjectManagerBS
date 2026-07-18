// @vitest-environment jsdom

import { describe, expect, it } from 'vitest';

import {
  isSafeMarkdownUrl,
  normalizeProjectManagementMarkdown,
  renderProjectManagementMarkdown,
  sanitizeProjectManagementPaste
} from './projectManagementMarkdown';

describe('project management markdown security policy', () => {
  it('keeps the safe subset and removes raw HTML, handlers, scripts, and unsafe links', () => {
    const value = normalizeProjectManagementMarkdown('**safe** <img src=x onerror=alert(1)> <script>alert(1)</script> [bad](javascript:alert(1)) [good](https://example.com)');
    expect(value).toContain('**safe**');
    expect(value).toContain('[good](https://example.com)');
    expect(value).not.toMatch(/script|onerror|javascript:|<img/i);
    expect(value).toContain('bad');
  });

  it('allows only http, https, and mailto destinations', () => {
    expect(isSafeMarkdownUrl('https://example.com/a')).toBe(true);
    expect(isSafeMarkdownUrl('mailto:user@example.com')).toBe(true);
    expect(isSafeMarkdownUrl('javascript:alert(1)')).toBe(false);
    expect(isSafeMarkdownUrl('data:text/html,<script>alert(1)</script>')).toBe(false);
    expect(isSafeMarkdownUrl('//evil.example')).toBe(false);
  });

  it('converts rich clipboard input to cleaned text before insertion', () => {
    expect(sanitizeProjectManagementPaste('', '<p>safe <strong>text</strong></p><img src=x onerror=alert(1)><script>alert(1)</script>')).toBe('safe text');
  });

  it('sanitizes the final rendered HTML even when the source contains an XSS payload', () => {
    const html = renderProjectManagementMarkdown('# title\n\n[attack](javascript:alert(1)) **safe** <svg onload=alert(1)>');
    expect(html).toContain('<h1>title</h1>');
    expect(html).toContain('<strong>safe</strong>');
    expect(html).not.toMatch(/script|onload|javascript:|svg|<img/i);
  });
});
