// @vitest-environment jsdom

import { describe, expect, it } from 'vitest';

import {
  designerRuntimeContentSecurityPolicy,
  isSafeRuntimeUrl,
  resolveSafeRuntimeUrl,
  resolvePublishedApiRoute,
  sanitizeDesignerRuntimeHtml
} from './designerRuntimeSecurityPolicy';

describe('designer runtime security policy', () => {
  it('sanitizes executable markup, event handlers, dangerous URLs, and embed primitives', () => {
    const result = sanitizeDesignerRuntimeHtml('<img src="javascript:alert(1)" onerror="alert(1)"><script>alert(1)</script><object data="/evil"></object><b>safe</b>');
    expect(result).not.toMatch(/script|onerror|javascript:|object/i);
    expect(result).toContain('<b>safe</b>');
  });

  it('applies the same URL policy to runtime resource attributes', () => {
    expect(resolveSafeRuntimeUrl('/uploads/image.png')).toBe('/uploads/image.png');
    expect(resolveSafeRuntimeUrl('https://example.com/image.png')).toBe('https://example.com/image.png');
    expect(resolveSafeRuntimeUrl('javascript:alert(1)')).toBe('');
    expect(isSafeRuntimeUrl('data:text/html,<script>alert(1)</script>')).toBe(false);
  });

  it('only resolves a route from a published API binding', () => {
    expect(resolvePublishedApiRoute({ id: 'api-1', config: { status: 'published', routePath: '/api/orders' } })).toBe('/api/orders');
    expect(resolvePublishedApiRoute({ id: 'api-1', config: { status: 'draft', routePath: '/api/orders' } })).toBeNull();
    expect(resolvePublishedApiRoute({ id: 'api-1', config: { published: true, routePath: 'https://evil.example' } })).toBeNull();
  });

  it('declares a restrictive runtime CSP baseline', () => {
    expect(designerRuntimeContentSecurityPolicy).toContain("object-src 'none'");
    expect(designerRuntimeContentSecurityPolicy).toContain("connect-src 'self'");
  });
});
