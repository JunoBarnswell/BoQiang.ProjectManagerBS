// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { DesignerCommandBus } from '../commands/DesignerCommandBus';
import type { DesignerDocument } from '../document/DesignerDocument';

import { WorkflowBindingPanel } from './WorkflowBindingPanel';

function documentFixture(): DesignerDocument { return { actions: [], apiBindings: [], dataSources: [], documentId: 'orders', elements: { root: { children: [], events: [], id: 'root', layout: {}, parentId: null, props: {}, type: 'layout.page' } }, metadata: {}, modals: [], pageParameters: [], pages: [{ id: 'orders', name: '订单', rootElementId: 'root' }], pageType: 'standard', permissions: {}, revision: 1, runtimeContext: {}, styleTokens: {}, variables: [], workflowBindings: [] }; }

describe('latest WorkflowBindingPanel', () => {
  afterEach(() => cleanup());
  it('exposes the host-callable fields and emits a validated document on save', () => {
    const onDocumentChange = vi.fn();
    render(<WorkflowBindingPanel commandBus={new DesignerCommandBus(documentFixture())} document={documentFixture()} page={{ pageCode: 'orders', pageName: '订单' }} definitions={[{ id: 'definition-1', key: 'order.approve', name: '订单审批', version: 2 }]} onCommandResult={onDocumentChange} />);
    fireEvent.change(screen.getByLabelText('审批流定义'), { target: { value: 'definition-1' } });
    fireEvent.click(screen.getByRole('button', { name: '保存工作流绑定' }));
    expect(onDocumentChange).toHaveBeenCalledOnce();
    expect(onDocumentChange.mock.calls[0][0].document.workflowBindings).toHaveLength(1);
  });

  it('blocks enabled save when no definition is selected', () => {
    const onDocumentChange = vi.fn();
    render(<WorkflowBindingPanel commandBus={new DesignerCommandBus(documentFixture())} document={documentFixture()} page={{ pageCode: 'orders', pageName: '订单' }} definitions={[]} onCommandResult={onDocumentChange} />);
    fireEvent.click(screen.getByRole('button', { name: '保存工作流绑定' }));
    expect(onDocumentChange).not.toHaveBeenCalled();
    expect(screen.getAllByRole('alert').length).toBeGreaterThan(0);
  });
});
