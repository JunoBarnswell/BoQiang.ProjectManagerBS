/** @vitest-environment jsdom */

import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { WorkflowBpmnImportDialog } from './WorkflowBpmnImportDialog';
import { createDefaultBusinessDesign } from './workflowBusinessModel';

vi.mock('../../../core/i18n/I18nProvider', () => ({
  useI18n: () => ({ translate: (key: string) => key })
}));

const translate = (key: string) => key;

describe('WorkflowBpmnImportDialog', () => {
  afterEach(() => cleanup());

  it('shows a real DOM diff and requires explicit confirmation before replacement', async () => {
    const user = userEvent.setup();
    const onConfirm = vi.fn();
    const design = createDefaultBusinessDesign(translate);
    const xml = `<?xml version="1.0"?><definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"><bpmn:process id="p"><bpmn:startEvent id="start"/><bpmn:endEvent id="end"/><bpmn:sequenceFlow id="flow" sourceRef="start" targetRef="end"/></bpmn:process></definitions>`;

    render(<WorkflowBpmnImportDialog currentDesign={design} initialXml={xml} onCancel={vi.fn()} onConfirm={onConfirm} />);

    expect(screen.getByRole('dialog')).toBeTruthy();
    expect(screen.getByText('page.workflowDesigner.bpmnImport.diff')).toBeTruthy();
    expect((screen.getByRole('button', { name: 'page.workflowDesigner.bpmnImport.confirm' }) as HTMLButtonElement).disabled).toBe(false);

    await user.click(screen.getAllByRole('button', { name: 'page.workflowDesigner.bpmnImport.cancel' })[1]);
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('renders unsupported elements and disables replacement', () => {
    const design = createDefaultBusinessDesign(translate);
    const xml = '<definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"><bpmn:process id="p"><bpmn:serviceTask id="service"/></bpmn:process></definitions>';

    render(<WorkflowBpmnImportDialog currentDesign={design} initialXml={xml} onCancel={vi.fn()} onConfirm={vi.fn()} />);

    expect(screen.getByText('page.workflowDesigner.bpmnImport.unsupported')).toBeTruthy();
    expect((screen.getByRole('button', { name: 'page.workflowDesigner.bpmnImport.confirm' }) as HTMLButtonElement).disabled).toBe(true);
  });
});
