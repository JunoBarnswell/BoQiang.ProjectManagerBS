import { describe, expect, it } from 'vitest';

import { generateBpmnFromBusinessDesign } from './workflowBusinessBpmn';
import { createDefaultBusinessDesign, normalizeBusinessDesign, type WorkflowBusinessDesign } from './workflowBusinessModel';

const t = (key: string) => key;

describe('workflowBusinessBpmn', () => {
  it('writes built-in dynamic approver as assignee expression', () => {
    const design = withApprovalNode((node) => ({
      ...node,
      participantType: 'starterManager'
    }));

    const xml = generateBpmnFromBusinessDesign(design, 'order_approval', 'Order Approval');

    expect(xml).toContain('activiti:assignee="${starterManagerUserId}"');
  });

  it('writes role approver as candidate group', () => {
    const design = withApprovalNode((node) => ({
      ...node,
      participantId: 'role-admin',
      participantType: 'role'
    }));

    const xml = generateBpmnFromBusinessDesign(design, 'order_approval', 'Order Approval');

    expect(xml).toContain('activiti:candidateGroups="role:role-admin"');
  });

  it('writes department manager approver as candidate users expression', () => {
    const design = withApprovalNode((node) => ({
      ...node,
      participantType: 'deptManager'
    }));

    const xml = generateBpmnFromBusinessDesign(design, 'order_approval', 'Order Approval');

    expect(xml).toContain('activiti:candidateUsers="${starterDeptManagerUserIds}"');
    expect(xml).not.toContain('activiti:assignee="${starterDeptManagerUserId}"');
  });

  it('writes multi approver configuration as standard multi-instance child', () => {
    const design = withApprovalNode((node) => ({
      ...node,
      approvalMode: 'any',
      participantIds: ['user-a', 'user-b'],
      participantNames: ['User A', 'User B']
    }));

    const xml = generateBpmnFromBusinessDesign(design, 'order_approval', 'Order Approval');

    expect(xml).toContain('<bpmn:multiInstanceLoopCharacteristics activiti:collectionVariable="approveTask_approvers" activiti:elementVariable="approver">');
    expect(xml).toContain('&quot;participantCollectionVariable&quot;:&quot;approveTask_approvers&quot;');
    expect(xml).not.toContain('activiti:collection="${candidateUsers}"');
  });
});

function withApprovalNode(
  updater: (node: WorkflowBusinessDesign['nodes'][number]) => WorkflowBusinessDesign['nodes'][number]
): WorkflowBusinessDesign {
  const design = normalizeBusinessDesign(createDefaultBusinessDesign(t), t);
  return {
    ...design,
    nodes: design.nodes.map((node) => node.type === 'approval' ? updater(node) : node)
  };
}
