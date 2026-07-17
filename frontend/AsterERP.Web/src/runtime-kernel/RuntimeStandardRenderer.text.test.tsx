// @vitest-environment jsdom

import { cleanup, render } from '@testing-library/react';
import type { ReactNode } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import type { RuntimeComponentRenderContext } from './RuntimeComponentTypes';
import { hasStandardRuntimeRenderer, renderStandardRuntime } from './RuntimeStandardRenderer';

afterEach(cleanup);

const textTags = [
  ['text', 'SPAN'],
  ['text.paragraph', 'P'],
  ['text.heading', 'H2'],
  ['text.h1', 'H1'],
  ['text.h2', 'H2'],
  ['text.h3', 'H3'],
  ['text.h4', 'H4'],
  ['text.h5', 'H5'],
  ['text.h6', 'H6'],
  ['text.link', 'A'],
  ['text.em', 'EM'],
  ['text.strong', 'STRONG'],
  ['text.small', 'SMALL'],
  ['text.mark', 'MARK'],
  ['text.blockquote', 'BLOCKQUOTE'],
  ['text.quote', 'Q'],
  ['text.code', 'CODE'],
  ['text.pre', 'PRE'],
  ['text.br', 'BR'],
  ['text.hr', 'HR'],
  ['text.time', 'TIME'],
] as const;

describe('Text standard runtime semantics', () => {
  it('registers and renders all 21 text component types with native tags', () => {
    for (const [componentType, tag] of textTags) {
      expect(hasStandardRuntimeRenderer(componentType)).toBe(true);
      const { container } = render(renderStandardRuntime(context(componentType, { text: 'Text' })));

      expect(container.firstElementChild?.tagName).toBe(tag);
      cleanup();
    }
  });

  it('renders props.text as text content and preserves link href and target', () => {
    const { container } = render(renderStandardRuntime(context('text', { content: 'Content prop', text: 'Text prop' })));
    expect(container.firstElementChild?.textContent).toBe('Text prop');
    cleanup();

    const link = render(renderStandardRuntime(context('text.link', { href: '/docs', target: '_blank', text: 'Docs' }))).container.firstElementChild as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('/docs');
    expect(link.getAttribute('target')).toBe('_blank');
    expect(link.getAttribute('rel')).toBe('noreferrer');
  });

  it('does not use content or title as a fallback for text components', () => {
    const { container } = render(renderStandardRuntime(context('text.paragraph', { content: 'Content prop', title: 'Title prop' })));

    expect(container.firstElementChild?.textContent).toBe('');
  });

  it('renders br and hr as void elements without fabricated text children', () => {
    for (const componentType of ['text.br', 'text.hr'] as const) {
      const { container } = render(renderStandardRuntime(context(componentType, { text: 'Ignored' }, ['Unexpected child'])));
      const element = container.firstElementChild;

      expect(element?.tagName).toBe(componentType === 'text.br' ? 'BR' : 'HR');
      expect(element?.childNodes).toHaveLength(0);
      expect(element?.textContent).toBe('');
      cleanup();
    }
  });
});

function context(componentType: string, props: Record<string, unknown>, children: ReactNode[] = []): RuntimeComponentRenderContext {
  return {
    bindings: {},
    children,
    componentType,
    disabled: false,
    element: { children: [], events: [], id: componentType, layout: {}, name: componentType, parentId: null, props, style: {}, type: componentType },
    executeAction: vi.fn(),
    layout: {},
    loading: false,
    onChange: vi.fn(),
    props,
    readOnly: false,
    runtime: {} as RuntimeComponentRenderContext['runtime'],
    scope: {},
    style: {},
    title: componentType,
    value: undefined,
    visible: true,
  };
}
