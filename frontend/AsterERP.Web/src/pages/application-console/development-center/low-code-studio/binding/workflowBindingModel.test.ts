import { describe, expect, it } from 'vitest';

import type { DesignerDocument } from '../document/DesignerDocument';

import { applyWorkflowBindingDraft, readWorkflowBindingDraft, validateWorkflowBindingDraft } from './workflowBindingModel';

const page = { pageCode: 'orders', pageName: '订单', keyField: 'id', businessType: 'order', menuCode: 'orders' };
function documentFixture(): DesignerDocument { return { actions: [], apiBindings: [], dataSources: [], documentId: 'orders', elements: { action: { id: 'action', type: 'workflow.actions', parentId: null, children: [], layout: {}, props: {}, style: {}, bindings: {}, events: [] } }, metadata: {}, modals: [], pageParameters: [], pages: [{ id: 'orders', name: '订单', rootElementId: 'action' }], permissions: {}, revision: 1, runtimeContext: {}, styleTokens: {}, variables: [], workflowBindings: [] }; }

describe('latest workflow binding model', () => {
  it('validates and persists one workflow binding plus the start event', () => {
    const document = documentFixture();
    const draft = { ...readWorkflowBindingDraft(document, page), processDefinitionId: 'definition-1', processDefinitionKey: 'order.approve', processDefinitionName: '订单审批', processDefinitionVersion: 2 };
    expect(validateWorkflowBindingDraft(draft)).toEqual([]);
    const result = applyWorkflowBindingDraft(document, page, draft);
    expect(result.errors).toEqual([]);
    expect(result.document.workflowBindings).toHaveLength(1);
    expect(result.document.elements.action.events).toHaveLength(1);
  });

  it('does not mutate the document when enabled configuration is incomplete', () => {
    const document = documentFixture();
    const result = applyWorkflowBindingDraft(document, page, { ...readWorkflowBindingDraft(document, page), processDefinitionId: '', processDefinitionKey: '' });
    expect(result.errors.length).toBeGreaterThan(0);
    expect(result.document.workflowBindings).toHaveLength(0);
  });
});
