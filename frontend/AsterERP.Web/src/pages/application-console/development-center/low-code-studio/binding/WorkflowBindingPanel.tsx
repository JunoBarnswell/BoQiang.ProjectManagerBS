import { Save } from 'lucide-react';
import { useMemo, useState, type ChangeEvent } from 'react';

import { translateCurrentLiteral } from '../../../../../core/i18n/I18nProvider';
import { createApplyWorkflowBindingCommand } from '../commands/createDesignerCommands';
import type { DesignerCommandBus } from '../commands/DesignerCommandBus';
import type { DesignerDocument } from '../document/DesignerDocument';
import type { ExpressionGraph } from '../expression/expressionGraph';
import { ExpressionGraphEditor } from '../expression/ExpressionGraphEditor';

import { readWorkflowBindingDraft, validateWorkflowBindingDraft } from './workflowBindingModel';
import type { WorkflowBindingDraft, WorkflowDefinitionOption, WorkflowPageContext } from './workflowBindingTypes';

export interface WorkflowBindingPanelProps {
  document: DesignerDocument;
  page: WorkflowPageContext;
  definitions: readonly WorkflowDefinitionOption[];
  commandBus: DesignerCommandBus;
  onCommandResult?: (result: ReturnType<DesignerCommandBus['execute']>) => void;
  onSave?: (draft: WorkflowBindingDraft, document: DesignerDocument) => Promise<void> | void;
  disabled?: boolean;
}

export function WorkflowBindingPanel({ document, page, definitions, commandBus, onCommandResult, onSave, disabled = false }: WorkflowBindingPanelProps) {
  const initial = useMemo(() => readWorkflowBindingDraft(document, page), [document, page]);
  const [draft, setDraft] = useState<WorkflowBindingDraft>(initial);
  const [errors, setErrors] = useState<string[]>([]);
  const graph = useMemo<ExpressionGraph>(() => ({ root: { kind: 'literal', value: draft.businessKeyExpression, valueType: 'string' } }), [draft.businessKeyExpression]);
  const patch = <K extends keyof WorkflowBindingDraft>(key: K, value: WorkflowBindingDraft[K]) => setDraft((current) => ({ ...current, [key]: value, syncStatus: 'pendingPublish' }));
  const definition = definitions.find((item) => item.id === draft.processDefinitionId);
  const save = async () => {
    const validationErrors = validateWorkflowBindingDraft(draft);
    setErrors(validationErrors);
    if (validationErrors.length) return;
    const result = commandBus.execute(createApplyWorkflowBindingCommand(page, draft));
    if (!result.changed) { setErrors([...result.diagnostics]); return; }
    onCommandResult?.(result);
    await onSave?.(draft, result.document);
  };
  const updateText = (key: keyof WorkflowBindingDraft) => (event: ChangeEvent<HTMLInputElement>) => patch(key, event.target.value as WorkflowBindingDraft[typeof key]);
  return <section className="space-y-3 rounded-lg border border-slate-200 bg-white p-3" aria-label={translateCurrentLiteral('工作流绑定')}>
    <header><p className="text-[10px] font-semibold uppercase tracking-wider text-slate-400">Workflow Binding</p><h2 className="text-sm font-semibold text-slate-900">{translateCurrentLiteral('页面审批配置')}</h2><p className="text-[10px] text-slate-500">{translateCurrentLiteral('配置保存到最新 DesignerDocument.workflowBindings，并在发布时同步。')}</p></header>
    <label className="flex items-center gap-2 text-xs text-slate-700"><input type="checkbox" checked={draft.enabled} disabled={disabled} onChange={(event) => patch('enabled', event.target.checked)} />{translateCurrentLiteral('启用页面审批')}</label>
    <label className="block text-xs text-slate-600">{translateCurrentLiteral('审批流定义')}<select className="form-select mt-1 h-8 w-full text-xs" disabled={disabled} value={draft.processDefinitionId} onChange={(event) => { const selected = definitions.find((item) => item.id === event.target.value); setDraft((current) => ({ ...current, processDefinitionId: selected?.id ?? '', processDefinitionKey: selected?.key ?? '', processDefinitionName: selected?.name ?? '', processDefinitionVersion: selected?.version ?? null, syncStatus: 'pendingPublish' })); }}><option value="">{translateCurrentLiteral('请选择审批流')}</option>{definitions.map((item) => <option key={item.id} value={item.id}>{item.name} · {item.key} v{item.version}</option>)}</select></label>
    {definition ? <p className="text-[10px] text-slate-500">{translateCurrentLiteral('已选：')}{definition.name}（{definition.key} / v{definition.version}）</p> : null}
    <label className="block text-xs text-slate-600">{translateCurrentLiteral('业务类型')}<input className="form-input mt-1 h-8 w-full text-xs" disabled={disabled} value={draft.businessType} onChange={updateText('businessType')} /></label>
    <label className="block text-xs text-slate-600">{translateCurrentLiteral('菜单编码')}<input className="form-input mt-1 h-8 w-full text-xs" disabled={disabled} value={draft.menuCode} onChange={updateText('menuCode')} /></label>
    <label className="block text-xs text-slate-600">{translateCurrentLiteral('标题模板')}<input className="form-input mt-1 h-8 w-full text-xs" disabled={disabled} value={draft.titleTemplate} onChange={updateText('titleTemplate')} /></label>
    <div><span className="text-xs text-slate-600">{translateCurrentLiteral('业务 Key 节点图')}</span><ExpressionGraphEditor graph={graph} expectedType="string" onChange={(next) => patch('businessKeyExpression', expressionText(next))} compact /></div>
    <label className="block text-xs text-slate-600">{translateCurrentLiteral('业务 Key 当前值')}<input className="form-input mt-1 h-8 w-full text-xs" disabled={disabled} value={draft.businessKeyExpression} onChange={updateText('businessKeyExpression')} /></label>
    {errors.map((error) => <p key={error} role="alert" className="text-xs text-red-600">{error}</p>)}
    <button type="button" disabled={disabled} className="inline-flex items-center gap-1 rounded bg-primary-600 px-3 py-1.5 text-xs font-medium text-white disabled:opacity-50" onClick={() => void save()}><Save className="h-3.5 w-3.5" />{translateCurrentLiteral('保存工作流绑定')}</button>
  </section>;
}

function expressionText(graph: ExpressionGraph): string {
  const root = graph.root;
  if (!root) return '';
  if (root.kind === 'literal') return typeof root.value === 'string' ? root.value : JSON.stringify(root.value);
  if (root.kind === 'resourceRef') return `{{${root.resourceId}}}`;
  if (root.kind === 'conversion' || root.kind === 'functionCall') return `{{${root.kind}:${root.kind === 'conversion' ? root.pipeline[0]?.name ?? '' : root.functionId}}}`;
  if (root.kind === 'logic') return `{{logic:${root.operator}}}`;
  if (root.kind === 'condition') return '{{condition}}';
  return '{{default}}';
}
