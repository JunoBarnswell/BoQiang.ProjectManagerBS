// @vitest-environment jsdom

import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { renderChoiceRuntime } from './RuntimeChoiceRenderer';
import type { RuntimeComponentRenderContext } from './RuntimeComponentTypes';

afterEach(cleanup);

describe('Runtime choice renderer', () => {
  it('keeps static options when a form binding resolves to a scalar value', () => {
    render(renderChoiceRuntime(context({ data: 'Draft' })));

    expect(screen.getByRole('option', { name: 'Draft' })).toBeTruthy();
    expect(screen.getByRole('option', { name: '已保存' })).toBeTruthy();
  });

  it('uses an options collection supplied by a runtime binding', () => {
    render(renderChoiceRuntime(context({ options: [{ label: '运行时', value: 'runtime' }] })));

    expect(screen.getByRole('option', { name: '运行时' })).toBeTruthy();
    expect(screen.queryByRole('option', { name: 'Draft' })).toBeNull();
  });
});

function context(bindings: Record<string, unknown>): RuntimeComponentRenderContext {
  return {
    bindings,
    children: [],
    componentType: 'select.dropdown',
    disabled: false,
    element: { children: [], events: [], id: 'status', name: 'Status', parentId: null, props: {}, style: {}, type: 'select.dropdown' },
    executeAction: vi.fn(),
    layout: {},
    loading: false,
    onChange: vi.fn(),
    props: { options: [{ label: 'Draft', value: 'Draft' }, { label: '已保存', value: '已保存' }] },
    readOnly: false,
    runtime: {} as RuntimeComponentRenderContext['runtime'],
    scope: {},
    style: {},
    title: '状态',
    value: '',
    visible: true
  };
}
