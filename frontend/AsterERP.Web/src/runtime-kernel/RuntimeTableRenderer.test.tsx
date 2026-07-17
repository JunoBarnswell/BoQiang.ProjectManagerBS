// @vitest-environment jsdom

import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import type { RuntimeComponentRenderContext } from './RuntimeComponentTypes';
import { renderStandardRuntime } from './RuntimeStandardRenderer';
import { renderTableRuntime } from './RuntimeTableRenderer';

afterEach(cleanup);

describe('Runtime semantic table capability', () => {
  it('renders data with real table section semantics', () => {
    render(renderTableRuntime(context()));
    expect(screen.getByRole('table')).toBeTruthy();
    expect(screen.getByRole('columnheader', { name: 'Name' }).getAttribute('scope')).toBe('col');
    expect(screen.getByRole('cell', { name: 'Ada' })).toBeTruthy();
    expect(screen.getAllByRole('rowgroup').length).toBe(2);
  });

  it('maps semantic table component nodes to native HTML tags', () => {
    const base = context();
    for (const [componentType, tag] of [['table.thead', 'THEAD'], ['table.tbody', 'TBODY'], ['table.tr', 'TR'], ['table.th', 'TH'], ['table.td', 'TD']] as const) {
      const { container } = render(renderStandardRuntime({ ...base, componentType, children: ['content'] }));
      expect(container.firstElementChild?.tagName).toBe(tag);
      cleanup();
    }
  });
});

function context(): RuntimeComponentRenderContext {
  return {
    bindings: { data: [{ name: 'Ada', amount: 4 }] }, children: [], componentType: 'table.semantic', disabled: false,
    element: { children: [], events: [], id: 'table', name: 'Table', parentId: null, props: {}, style: {}, type: 'table.semantic' }, executeAction: vi.fn(), layout: {}, loading: false, onChange: vi.fn(), props: { columns: [{ key: 'name', title: 'Name' }, { key: 'amount', title: 'Amount' }], caption: 'Orders' }, readOnly: false,
    runtime: {} as RuntimeComponentRenderContext['runtime'], scope: {}, style: {}, title: 'Orders', value: [], visible: true
  };
}
