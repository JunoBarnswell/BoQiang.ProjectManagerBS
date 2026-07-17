// @vitest-environment jsdom

import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { renderChartRuntime } from './RuntimeChartRenderer';
import type { RuntimeComponentRenderContext } from './RuntimeComponentTypes';

afterEach(cleanup);

describe('RuntimeChartRenderer capability contract', () => {
  it('renders configured series, axes, legend, tooltip and chart type', () => {
    render(renderChartRuntime(context({ chartType: 'line', series: [{ name: 'Orders', data: [{ month: 'Jan', amount: 4 }, { month: 'Feb', amount: 8 }] }], axis: { xKey: 'month', yKey: 'amount', xLabel: 'Month', yLabel: 'Orders' } })));
    expect(screen.getByLabelText('Sales').getAttribute('data-chart-type')).toBe('line');
    expect(screen.getByRole('img').getAttribute('aria-label')).toContain('Orders Jan 4');
    expect(screen.getByText('Month')).toBeTruthy();
    expect(screen.getAllByText('Orders').length).toBeGreaterThan(0);
    expect(screen.getByRole('list', { name: 'Legend' }).textContent).toContain('Orders');
    expect(document.querySelector('title')?.textContent).toBe('Orders: Jan 4');
  });

  it('keeps loading, error, and empty states explicit', () => {
    const { rerender } = render(renderChartRuntime(context({ loading: true })));
    expect(screen.getByRole('status').textContent).toContain('Loading');
    rerender(renderChartRuntime(context({ error: 'Request failed' })));
    expect(screen.getByRole('alert').textContent).toContain('Request failed');
    rerender(renderChartRuntime(context({ data: [] })));
    expect(screen.getByText('No chart data')).toBeTruthy();
  });
});

function context(overrides: Record<string, unknown>): RuntimeComponentRenderContext {
  return {
    children: [], componentType: 'chart.basic', disabled: false,
    element: { children: [], events: [], id: 'chart', name: 'Chart', parentId: null, props: {}, style: {}, type: 'chart.basic' },
    executeAction: vi.fn(), layout: {}, loading: overrides.loading === true, onChange: vi.fn(), props: { data: [{ label: 'Jan', value: 1 }], ...overrides }, readOnly: false,
    runtime: {} as RuntimeComponentRenderContext['runtime'], scope: {}, style: {}, title: 'Sales', value: [], visible: true
  };
}
