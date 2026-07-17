import { useMemo, useState } from 'react';

import { useI18n } from '../../../core/i18n/I18nProvider';

import { importBpmnToBusinessDesign } from './workflowBusinessBpmnImport';
import type { WorkflowBusinessDesign } from './workflowBusinessModel';

interface WorkflowBpmnImportDialogProps {
  currentDesign: WorkflowBusinessDesign;
  initialXml?: string;
  onCancel: () => void;
  onConfirm: (design: WorkflowBusinessDesign) => void;
}

export function WorkflowBpmnImportDialog({
  currentDesign,
  initialXml = '',
  onCancel,
  onConfirm
}: WorkflowBpmnImportDialogProps) {
  const { translate } = useI18n();
  const [sourceXml, setSourceXml] = useState(initialXml);
  const result = useMemo(() => importBpmnToBusinessDesign(sourceXml, currentDesign, translate), [currentDesign, sourceXml, translate]);
  const hasUnsupportedElements = result.unsupportedElements.length > 0;
  const canConfirm = Boolean(result.design) && !result.error && !hasUnsupportedElements;

  const readFile = async (file: File) => {
    setSourceXml(await file.text());
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" role="presentation">
      <section aria-labelledby="workflow-bpmn-import-title" aria-modal="true" className="w-full max-w-3xl max-h-[90vh] overflow-y-auto rounded-lg bg-white p-5 shadow-xl" role="dialog">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h2 className="text-lg font-semibold" id="workflow-bpmn-import-title">{translate('page.workflowDesigner.bpmnImport.title')}</h2>
            <p className="mt-1 text-sm text-amber-700">{translate('page.workflowDesigner.bpmnImport.irreversible')}</p>
          </div>
          <button aria-label={translate('page.workflowDesigner.bpmnImport.cancel')} className="text-gray-500" type="button" onClick={onCancel}>×</button>
        </div>

        <div className="mt-4 grid gap-3">
          <label className="grid gap-1 text-sm font-medium">
            {translate('page.workflowDesigner.bpmnImport.source')}
            <textarea className="min-h-48 rounded border border-gray-300 p-2 font-mono text-xs" value={sourceXml} onChange={(event) => setSourceXml(event.target.value)} />
          </label>
          <label className="text-sm">
            {translate('page.workflowDesigner.bpmnImport.file')}
            <input accept=".bpmn,.xml,application/xml,text/xml" className="ml-2" type="file" onChange={(event) => {
              const file = event.target.files?.[0];
              if (file) void readFile(file);
            }} />
          </label>
        </div>

        {result.error ? <div className="mt-3 rounded border border-red-200 bg-red-50 p-3 text-sm text-red-700">{result.error}</div> : null}

        {result.design ? (
          <div className="mt-4 grid gap-2 text-sm">
            <div className="font-semibold">{translate('page.workflowDesigner.bpmnImport.diff')}</div>
            <div>{translate('page.workflowDesigner.bpmnImport.addedNodes')}: {result.diff.addedNodeIds.length}</div>
            <div>{translate('page.workflowDesigner.bpmnImport.removedNodes')}: {result.diff.removedNodeIds.length}</div>
            <div>{translate('page.workflowDesigner.bpmnImport.changedNodes')}: {result.diff.changedNodeIds.length}</div>
            <div>{translate('page.workflowDesigner.bpmnImport.edgeChanges')}: {result.diff.addedEdgeIds.length + result.diff.removedEdgeIds.length}</div>
          </div>
        ) : null}

        {result.unsupportedElements.length > 0 ? (
          <div className="mt-4 rounded border border-red-200 bg-red-50 p-3 text-sm text-red-700">
            <div className="font-semibold">{translate('page.workflowDesigner.bpmnImport.unsupported')}</div>
            {result.unsupportedElements.map((element) => <div key={`${element.id}:${element.type}`}>{element.id} ({element.type}): {element.reason}</div>)}
          </div>
        ) : null}

        {result.warnings.length > 0 ? (
          <div className="mt-4 rounded border border-amber-200 bg-amber-50 p-3 text-sm text-amber-700">
            {result.warnings.map((warning) => <div key={warning}>{warning}</div>)}
          </div>
        ) : null}

        <div className="mt-5 flex justify-end gap-2">
          <button className="rounded border border-gray-300 px-3 py-1.5 text-sm" type="button" onClick={onCancel}>{translate('page.workflowDesigner.bpmnImport.cancel')}</button>
          <button className="rounded bg-primary-600 px-3 py-1.5 text-sm text-white disabled:cursor-not-allowed disabled:opacity-50" disabled={!canConfirm} type="button" onClick={() => {
            if (result.design) onConfirm(result.design);
          }}>{translate('page.workflowDesigner.bpmnImport.confirm')}</button>
        </div>
      </section>
    </div>
  );
}
