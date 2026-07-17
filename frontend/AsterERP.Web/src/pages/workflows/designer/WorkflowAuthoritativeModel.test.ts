import { describe, expect, it } from 'vitest';

import { createWorkflowAuthoritativeModel } from './WorkflowAuthoritativeModel';
import { createDefaultBusinessDesign } from './workflowBusinessModel';

const translate = (key: string) => key;

describe('WorkflowAuthoritativeModel', () => {
  it('always derives deterministic BPMN and extension data from the business model', () => {
    const design = createDefaultBusinessDesign(translate);
    const first = createWorkflowAuthoritativeModel(design, 'approval', 'Approval', translate);
    const second = createWorkflowAuthoritativeModel(design, 'approval', 'Approval', translate);

    expect(first.source).toBe('business');
    expect(first.extensionJson).toBe(second.extensionJson);
    expect(first.bpmnXml).toBe(second.bpmnXml);
    expect(first.extensionJson).not.toContain('updatedAt');
    expect(first.extensionJson).toContain('businessDesign');
    expect(first.extensionJson).toContain('WorkflowBusinessModelLatest');
    expect(first.extensionJson).toContain('"version":"latest"');
    expect(first.extensionJson).not.toContain('approvalNodes');
  });
});
