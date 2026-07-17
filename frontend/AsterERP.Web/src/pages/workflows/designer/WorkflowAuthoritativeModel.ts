import { generateBpmnFromBusinessDesign } from './workflowBusinessBpmn';
import type { TranslateFn } from './workflowBusinessI18n';
import { normalizeBusinessDesign, serializeBusinessDesign, type WorkflowBusinessDesign } from './workflowBusinessModel';

export interface WorkflowAuthoritativeModel {
  source: 'business';
  businessDesign: WorkflowBusinessDesign;
  bpmnXml: string;
  extensionJson: string;
}

export function createWorkflowAuthoritativeModel(
  design: WorkflowBusinessDesign,
  processId: string,
  processName: string,
  translate: TranslateFn
): WorkflowAuthoritativeModel {
  const businessDesign = normalizeBusinessDesign(design, translate);

  return {
    source: 'business',
    businessDesign,
    bpmnXml: generateBpmnFromBusinessDesign(businessDesign, processId, processName),
    extensionJson: serializeBusinessDesign(businessDesign, translate)
  };
}
