// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import type { RuntimeComponentRenderContext } from './RuntimeComponentTypes';
import { renderInputRuntime } from './RuntimeInputRenderer';

afterEach(cleanup);

describe('Runtime signature capability', () => {
  beforeEach(() => {
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({ clearRect: vi.fn(), beginPath: vi.fn(), moveTo: vi.fn(), lineTo: vi.fn(), stroke: vi.fn() } as unknown as CanvasRenderingContext2D);
    vi.spyOn(HTMLCanvasElement.prototype, 'getBoundingClientRect').mockReturnValue({ left: 0, top: 0, width: 640, height: 240, right: 640, bottom: 240, x: 0, y: 0, toJSON: vi.fn() });
    HTMLCanvasElement.prototype.setPointerCapture = vi.fn();
    HTMLCanvasElement.prototype.releasePointerCapture = vi.fn();
    HTMLCanvasElement.prototype.hasPointerCapture = vi.fn(() => true);
  });

  it('draws, commits a ResourceRef-compatible value, undoes and clears', () => {
    const onChange = vi.fn();
    render(renderInputRuntime(context(onChange)));
    const canvas = screen.getByLabelText('Signature');
    fireEvent.pointerDown(canvas, { pointerId: 1, clientX: 10, clientY: 10 });
    fireEvent.pointerMove(canvas, { pointerId: 1, clientX: 30, clientY: 30 });
    fireEvent.pointerUp(canvas, { pointerId: 1, clientX: 30, clientY: 30 });
    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ resourceId: 'signature:sign-1', resourceType: 'signature', valueType: 'json', strokes: expect.any(Array) }), undefined);
    fireEvent.click(screen.getByRole('button', { name: 'Undo signature stroke' }));
    fireEvent.click(screen.getByRole('button', { name: 'Clear signature' }));
    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ resourceId: 'signature:sign-1', strokes: [] }), undefined);
  });

  it('renders dates as manually enterable ISO date values', () => {
    const onChange = vi.fn();
    render(renderInputRuntime(context(onChange, 'input.date')));
    const input = screen.getByRole('textbox', { name: '订单日期' });
    expect(input.getAttribute('type')).toBe('text');
    expect(input.getAttribute('inputmode')).toBe('numeric');
    expect(input.getAttribute('placeholder')).toBe('YYYY-MM-DD');
    fireEvent.change(input, { target: { value: '2026-07-15' } });
    expect(onChange).toHaveBeenCalledWith('2026-07-15', undefined);
  });
});

function context(onChange: ReturnType<typeof vi.fn>, componentType: RuntimeComponentRenderContext['componentType'] = 'media.signature'): RuntimeComponentRenderContext {
  return {
    children: [], componentType, disabled: false, element: { children: [], events: [], id: 'sign-1', name: componentType === 'input.date' ? '订单日期' : 'Signature', parentId: null, props: {}, style: {}, type: componentType }, executeAction: vi.fn(), layout: {}, loading: false, onChange: onChange as RuntimeComponentRenderContext['onChange'], props: { penColor: '#111827' }, readOnly: false,
    runtime: {} as RuntimeComponentRenderContext['runtime'], scope: {}, style: {}, title: componentType === 'input.date' ? '订单日期' : 'Signature', value: null, visible: true
  };
}
