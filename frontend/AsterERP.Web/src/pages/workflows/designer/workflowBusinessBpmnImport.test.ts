/** @vitest-environment jsdom */

import { describe, expect, it } from 'vitest';

import { generateBpmnFromBusinessDesign } from './workflowBusinessBpmn';
import { importBpmnToBusinessDesign } from './workflowBusinessBpmnImport';
import { createDefaultBusinessDesign, normalizeBusinessDesign } from './workflowBusinessModel';

const translate = (key: string) => key;

describe('workflowBusinessBpmnImport', () => {
  it('round-trips supported business nodes, edges, positions and config', () => {
    const source = normalizeBusinessDesign(createDefaultBusinessDesign(translate), translate);
    const xml = generateBpmnFromBusinessDesign(source, 'order_approval', 'Order Approval');

    const result = importBpmnToBusinessDesign(xml, source, translate);

    expect(result.error).toBeUndefined();
    expect(result.unsupportedElements).toEqual([]);
    expect(result.design?.nodes.map((node) => [node.id, node.type, node.position])).toEqual([
      ['startEvent', 'start', { x: 40, y: 160 }],
      ['approveTask', 'approval', { x: 300, y: 150 }],
      ['endEvent', 'end', { x: 580, y: 160 }]
    ]);
    expect(result.design?.edges.map((edge) => edge.id)).toEqual(['flow_start_approve', 'flow_approve_end']);
    expect(result.design?.nodes.find((node) => node.id === 'approveTask')?.fieldPermissions[0].fieldKey).toBe('businessName');
  });

  it('reports unsupported BPMN elements and leaves confirmation impossible', () => {
    const source = createDefaultBusinessDesign(translate);
    const xml = `<?xml version="1.0"?><definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"><bpmn:process id="p"><bpmn:startEvent id="start"/><bpmn:serviceTask id="service" name="Not supported"/><bpmn:endEvent id="end"/><bpmn:sequenceFlow id="f1" sourceRef="start" targetRef="service"/><bpmn:sequenceFlow id="f2" sourceRef="service" targetRef="end"/></bpmn:process></definitions>`;

    const result = importBpmnToBusinessDesign(xml, source, translate);

    expect(result.design?.nodes.map((node) => node.id)).toEqual(['start', 'end']);
    expect(result.unsupportedElements).toContainEqual({ id: 'service', type: 'serviceTask', reason: 'Element type is not representable by the business designer.' });
    expect(result.unsupportedElements.some((element) => element.type === 'sequenceFlow')).toBe(true);
  });

  it('rejects malformed XML without mutating the current business design', () => {
    const source = createDefaultBusinessDesign(translate);
    const result = importBpmnToBusinessDesign('<bpmn:definitions>', source, translate);

    expect(result.design).toBeNull();
    expect(result.error).toBeTruthy();
    expect(source.nodes).toHaveLength(3);
  });
});
