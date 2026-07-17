// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { HttpError } from '../core/http/httpError';

import { RuntimeActionError } from './RuntimeActionError';
import type { RuntimeComponentRenderContext } from './RuntimeComponentTypes';
import { renderDataTableRuntime } from './RuntimeDataTableRenderer';
import type { RuntimeContext } from './RuntimeTypes';

afterEach(cleanup);

describe('RuntimeDataTableRenderer editing DOM contract', () => {
  it('keeps a controlled draft, commits Enter once, and ignores the following blur', async () => {
    const user = userEvent.setup();
    const { context, onChange, executeAction } = createContext({ rows: [{ id: 1, name: 'Ada' }] });
    render(renderDataTableRuntime(context));
    const input = screen.getByRole('textbox', { name: 'Name row 1' });

    await user.clear(input);
    await user.type(input, 'Grace');
    expect(screen.getByText('Unsaved changes')).toBeTruthy();
    await user.keyboard('{Enter}');
    await waitFor(() => expect(onChange).toHaveBeenCalledTimes(1));
    fireEvent.blur(input);
    expect(onChange).toHaveBeenCalledTimes(1);
    expect(executeAction).toHaveBeenCalledTimes(1);
    expect(executeAction.mock.calls[0]?.[1].variables).toMatchObject({
      currentRow: { id: 1, name: 'Grace' },
      fieldCode: 'name',
      originalValue: 'Ada',
      value: 'Grace'
    });
    expect((input as HTMLInputElement).value).toBe('Grace');
    expect(input.closest('[data-edit-state]')?.getAttribute('data-edit-state')).toBe('idle');
  });

  it('cancels with Escape and respects blurCommit=false', async () => {
    const user = userEvent.setup();
    const { context, onChange } = createContext({ commitOnBlur: false, rows: [{ id: 1, name: 'Ada' }] });
    render(renderDataTableRuntime(context));
    const input = screen.getByRole('textbox', { name: 'Name row 1' });

    await user.clear(input);
    await user.type(input, 'Draft');
    fireEvent.blur(input);
    expect(onChange).not.toHaveBeenCalled();
    expect((input as HTMLInputElement).value).toBe('Draft');
    await user.click(input);
    await user.keyboard('{Escape}');
    expect((input as HTMLInputElement).value).toBe('Ada');
    expect(onChange).not.toHaveBeenCalled();
  });

  it('validates number and JSON values in the real editor before saving', async () => {
    const user = userEvent.setup();
    const { context, onChange } = createContext({
      rows: [{ id: 1, amount: 4, payload: { valid: true } }],
      columns: [
        { fieldCode: 'amount', fieldName: 'Amount', dataType: 'number', required: true },
        { fieldCode: 'payload', fieldName: 'Payload', dataType: 'json', required: true }
      ]
    });
    render(renderDataTableRuntime(context));
    const amount = screen.getByRole('spinbutton', { name: 'Amount row 1' });
    const payload = screen.getByRole('textbox', { name: 'Payload row 1' });

    await user.clear(amount);
    await user.keyboard('{Enter}');
    expect(screen.getByRole('alert').textContent).toContain('A value is required.');
    expect(onChange).not.toHaveBeenCalled();
    await user.type(amount, '8');
    await user.keyboard('{Enter}');
    await waitFor(() => expect(onChange).toHaveBeenCalledTimes(1));

    await user.clear(payload);
    fireEvent.change(payload, { target: { value: '{' } });
    await user.keyboard('{Enter}');
    expect(screen.getByRole('alert').textContent).toContain('Enter valid JSON.');
    expect(onChange).toHaveBeenCalledTimes(1);
    fireEvent.change(payload, { target: { value: '{}' } });
    await user.keyboard('{Enter}');
    await waitFor(() => expect(onChange).toHaveBeenCalledTimes(2));
    expect(onChange.mock.calls[1]?.[0][0]).toMatchObject({ amount: 8, payload: {} });
  });

  it('locks the editor while saving and preserves a failed draft for retry', async () => {
    const user = userEvent.setup();
    let resolveAction: (() => void) | undefined;
    const { context, executeAction } = createContext({ rows: [{ id: 1, name: 'Ada' }] });
    executeAction.mockImplementationOnce(() => new Promise<RuntimeContext>((resolve) => { resolveAction = () => resolve(context.runtime); }));
    const { rerender } = render(renderDataTableRuntime(context));
    const input = screen.getByRole('textbox', { name: 'Name row 1' });
    await user.clear(input);
    await user.type(input, 'Saving');
    await user.keyboard('{Enter}');
    expect((input as HTMLInputElement).disabled).toBe(true);
    await user.keyboard('{Enter}');
    expect(executeAction).toHaveBeenCalledTimes(1);
    resolveAction?.();
    await waitFor(() => expect((input as HTMLInputElement).disabled).toBe(false));

    executeAction.mockRejectedValueOnce(new Error('temporary failure')).mockResolvedValueOnce(context.runtime);
    await user.clear(input);
    await user.type(input, 'Recoverable');
    await user.keyboard('{Enter}');
    expect((await screen.findByRole('alert')).textContent).toContain('temporary failure');
    expect((input as HTMLInputElement).value).toBe('Recoverable');
    await user.click(screen.getByRole('button', { name: 'Retry' }));
    await waitFor(() => expect(executeAction).toHaveBeenCalledTimes(3));
    rerender(renderDataTableRuntime(context));
  });

  it('shows server/local values for 409 and supports retry and overwrite contracts', async () => {
    const user = userEvent.setup();
    const conflict = new RuntimeActionError('save failed', 'failed', 'save-cell', 'runPageMicroflow', 1000, new HttpError({
      data: { conflict: true, canOverwrite: true, canRetry: true, conflictMessage: 'The row changed.', localValues: { id: 1, name: 'Local' }, serverValues: { id: 1, name: 'Server' } },
      message: 'Conflict',
      status: 409
    }));
    const { context, executeAction, onChange } = createContext({ rows: [{ id: 1, name: 'Ada' }] });
    executeAction.mockRejectedValueOnce(conflict).mockResolvedValueOnce(context.runtime);
    render(renderDataTableRuntime(context));
    const input = screen.getByRole('textbox', { name: 'Name row 1' });
    await user.clear(input);
    await user.type(input, 'Local');
    await user.keyboard('{Enter}');
    expect((await screen.findAllByText('The row changed.')).length).toBeGreaterThan(0);
    expect(screen.getByText('Server')).toBeTruthy();
    expect(screen.getByText('Local')).toBeTruthy();
    await user.click(screen.getByRole('button', { name: 'Retry with server base' }));
    await waitFor(() => expect(executeAction).toHaveBeenCalledTimes(2));
    expect(executeAction.mock.calls[1]?.[1].variables).toMatchObject({ conflictResolution: 'retry', originalRow: { id: 1, name: 'Server' } });
    await waitFor(() => expect(onChange).toHaveBeenCalledTimes(1));

    executeAction.mockRejectedValueOnce(conflict).mockResolvedValueOnce(context.runtime);
    await user.clear(input);
    await user.type(input, 'Overwrite');
    await user.keyboard('{Enter}');
    await screen.findAllByText('The row changed.');
    await user.click(screen.getByRole('button', { name: 'Overwrite server' }));
    await waitFor(() => expect(executeAction).toHaveBeenCalledTimes(4));
    expect(executeAction.mock.calls[3]?.[1].variables).toMatchObject({ conflictResolution: 'overwrite' });
  });

  it('does not render editors for primary keys, readonly columns, or a disabled runtime node', () => {
    const { context } = createContext({ disabled: true, rows: [{ id: 1, name: 'Ada' }], columns: [{ fieldCode: 'id', fieldName: 'Id' }, { fieldCode: 'name', fieldName: 'Name', readOnly: true }] });
    render(renderDataTableRuntime(context));
    expect(screen.queryByRole('textbox')).toBeNull();
    expect(screen.getByText('1')).toBeTruthy();
    expect(screen.getByText('Ada')).toBeTruthy();
  });

  it('renders destructive row actions in a modal and keeps cancel non-destructive', async () => {
    const user = userEvent.setup();
    const { context, executeAction } = createContext({
      rows: [{ id: 1, name: 'Ada' }],
      rowActions: [{ action: { id: 'delete-order', name: '删除', steps: [], trigger: 'click' }, confirmationMessage: '确定删除当前订单吗？', label: '删除', requiresConfirmation: true }]
    });

    render(renderDataTableRuntime(context));
    await user.click(screen.getByRole('button', { name: '删除' }));

    const dialog = screen.getByRole('dialog', { name: '删除' });
    expect(dialog.getAttribute('aria-modal')).toBe('true');
    expect(screen.getByText('确定删除当前订单吗？')).toBeTruthy();
    await user.click(screen.getByRole('button', { name: '取消' }));

    expect(executeAction).not.toHaveBeenCalled();
    expect(screen.queryByRole('dialog', { name: '删除' })).toBeNull();
  });

  it('executes the formal row action only after modal confirmation', async () => {
    const user = userEvent.setup();
    const { context, executeAction } = createContext({
      rows: [{ id: 7, name: 'Grace' }],
      rowActions: [{ action: { id: 'delete-order', name: '删除', steps: [], trigger: 'click' }, confirmationMessage: '确定删除当前订单吗？', label: '删除', requiresConfirmation: true }]
    });

    render(renderDataTableRuntime(context));
    await user.click(screen.getByRole('button', { name: '删除' }));
    expect(executeAction).not.toHaveBeenCalled();
    await user.click(screen.getByRole('button', { name: '确认' }));

    await waitFor(() => expect(executeAction).toHaveBeenCalledTimes(1));
    expect(executeAction.mock.calls[0]?.[1].variables).toMatchObject({ currentRow: { id: 7, name: 'Grace' } });
  });
});

interface ContextOptions {
  columns?: Array<Record<string, unknown>>;
  commitOnBlur?: boolean;
  disabled?: boolean;
  rows: Array<Record<string, unknown>>;
  rowActions?: Array<Record<string, unknown>>;
}

function createContext(options: ContextOptions): { context: RuntimeComponentRenderContext; executeAction: ReturnType<typeof vi.fn>; onChange: ReturnType<typeof vi.fn> } {
  const runtime = {
    closeModal: vi.fn(),
    componentValues: {},
    document: { elements: {}, modals: [], pages: [] },
    formValues: {},
    navigate: vi.fn(),
    openModal: vi.fn(),
    openPageInvocation: vi.fn(),
    openPrint: vi.fn(),
    refreshModel: vi.fn(async () => undefined),
    refreshVersion: 0,
    mergeVariables: vi.fn(),
    setFormValue: vi.fn(),
    setFormValues: vi.fn(),
    setVariable: vi.fn(),
    setVariablePath: vi.fn(),
    variables: {}
  } as unknown as RuntimeContext;
  const executeAction = vi.fn(async () => runtime);
  const onChange = vi.fn();
  const context = {
    bindings: { data: options.rows },
    children: [],
    componentType: 'report.dataTable',
    disabled: options.disabled === true,
    element: { children: [], events: [], id: 'table', name: 'Table', parentId: null, props: {}, style: {}, type: 'report.dataTable' },
    executeAction,
    layout: {},
    loading: false,
    onChange,
    permission: undefined,
    props: { columns: options.columns ?? [{ fieldCode: 'name', fieldName: 'Name', dataType: 'string' }], commitOnBlur: options.commitOnBlur ?? true, editable: true, rowActions: options.rowActions ?? [], rows: options.rows },
    readOnly: false,
    runtime,
    scope: {},
    style: {},
    title: 'Orders',
    value: options.rows,
    visible: true
  } as unknown as RuntimeComponentRenderContext;
  context.changeAction = { id: 'save-cell', name: 'Save cell', steps: [], trigger: 'change' };
  return { context, executeAction, onChange };
}
