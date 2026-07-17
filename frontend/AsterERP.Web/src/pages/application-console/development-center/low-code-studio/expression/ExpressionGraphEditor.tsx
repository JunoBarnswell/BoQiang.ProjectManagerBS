import { useCallback, useEffect, useMemo, useState } from 'react';

import { translateCurrentLiteral } from '../../../../../core/i18n/I18nProvider';
import { listStableResources } from '../binding/resourceExplorerStore';

import type { ExpressionGraph, ExpressionNode } from './expressionGraph';
import { diagnoseExpressionGraph, parseExpressionGraph, serializeExpressionGraph } from './expressionGraph';
import { createExpressionNode } from './expressionGraphEditorModel';
import { evaluateExpressionNode } from './expressionGraphEvaluator';
import { ExpressionNodeEditor } from './ExpressionNodeEditor';
import type { BindingDocument, DesignerValueType } from './expressionTypes';


export interface ExpressionGraphEditorProps {
  document?: BindingDocument | null;
  graph: ExpressionGraph;
  expectedType: DesignerValueType;
  onChange: (graph: ExpressionGraph) => void;
  onOpen?: () => void;
  compact?: boolean;
}

export function ExpressionGraphEditor({ document, graph, expectedType, onChange, onOpen, compact = false }: ExpressionGraphEditorProps) {
  const [open, setOpen] = useState(false);
  const [expert, setExpert] = useState(false);
  const [expertText, setExpertText] = useState(() => serializeExpressionGraph(graph));
  const [expertError, setExpertError] = useState<string | null>(null);
  const [importDiff, setImportDiff] = useState<string | null>(null);
  const [preview, setPreview] = useState<{ value?: unknown; error?: string }>({});
  const diagnostics = useMemo(() => diagnoseExpressionGraph(graph, expectedType), [graph, expectedType]);
  const resources = useMemo(() => listStableResources(document), [document]);

  useEffect(() => { if (!expert) setExpertText(serializeExpressionGraph(graph)); }, [expert, graph]);

  const updateGraph = (next: ExpressionGraph) => { onChange(next); setExpertText(serializeExpressionGraph(next)); };
  const addNode = (kind: ExpressionNode['kind']) => updateGraph({ root: wrapNode(kind, graph.root, expectedType) });
  const importExpertGraph = () => {
    try {
      const parsed = parseExpressionGraph(JSON.parse(expertText));
      if (!parsed) throw new Error('The JSON does not contain a valid Expression AST.');
      const errors = diagnoseExpressionGraph(parsed, expectedType);
      if (errors.length > 0) throw new Error(errors[0].message);
      const before = serializeExpressionGraph(graph);
      const after = serializeExpressionGraph(parsed);
      setImportDiff(before === after ? 'No AST changes.' : `AST diff: ${before.length} -> ${after.length} characters.`);
      setExpertError(null);
      updateGraph(parsed);
    } catch (error) {
      setExpertError(error instanceof Error ? error.message : 'Unable to import the Expression AST.');
    }
  };
  const runPreview = useCallback(() => {
    try {
      const value = evaluateExpressionNode(graph.root, { resolveResource: (resourceId) => resources.find((resource) => resource.id === resourceId)?.expression.value });
      setPreview({ value });
    } catch (error) {
      setPreview({ error: error instanceof Error ? error.message : 'Expression preview failed.' });
    }
  }, [graph, resources]);
  useEffect(() => { if (open) runPreview(); }, [open, runPreview]);
  const openLabel = translateCurrentLiteral('\u6253\u5f00\u8868\u8fbe\u5f0f\u8282\u70b9\u56fe');
  const conditionLabel = translateCurrentLiteral('\u6dfb\u52a0\u6761\u4ef6\u8282\u70b9');
  const expertLabel = translateCurrentLiteral('\u4e13\u5bb6\u6587\u672c');

  return <div className={compact ? 'relative' : 'rounded border border-slate-200 bg-white p-2'}>
    <button type="button" onClick={() => { setOpen((current) => !current); onOpen?.(); }} className="rounded border border-slate-300 px-2 py-1 text-xs" aria-expanded={open}>{openLabel}</button>
    {open ? <div className="mt-2 space-y-2" data-testid="expression-graph-editor">
      <div className="flex flex-wrap gap-1"><button type="button" aria-label={conditionLabel} onClick={() => addNode('condition')}>{translateCurrentLiteral('\u6761\u4ef6')}</button><button type="button" onClick={() => addNode('logic')}>Add logic</button><button type="button" onClick={() => addNode('conversion')}>Add conversion</button><button type="button" onClick={() => addNode('functionCall')}>Add function</button><button type="button" onClick={() => addNode('defaultValue')}>Add default</button><button type="button" aria-label={translateCurrentLiteral('\u5207\u6362\u4e13\u5bb6\u6587\u672c\u6a21\u5f0f')} onClick={() => { setExpert((current) => !current); setExpertError(null); }}>{expertLabel}</button><button type="button" onClick={runPreview}>Preview</button></div>
      {expert ? <div className="space-y-1"><textarea aria-label={expertLabel} value={expertText} onChange={(event) => { setExpertText(event.target.value); setImportDiff(null); try { const parsed = parseExpressionGraph(JSON.parse(event.target.value)); if (!parsed) throw new Error('Invalid Expression AST.'); const errors = diagnoseExpressionGraph(parsed, expectedType); if (errors.length) throw new Error(errors[0].message); setExpertError(null); updateGraph(parsed); } catch (error) { setExpertError(error instanceof Error ? error.message : 'Invalid Expression AST.'); } }} className="min-h-32 w-full rounded border border-slate-300 p-2 font-mono text-xs" /><div className="flex items-center gap-2"><button type="button" onClick={importExpertGraph}>Import AST</button>{importDiff ? <span className="text-[10px] text-slate-500">{importDiff}</span> : null}</div></div> : graph.root ? <><p className="text-[10px] text-slate-500">{translateCurrentLiteral('AST / \u8282\u70b9\u56fe')}</p><ExpressionNodeEditor document={document} expectedType={expectedType} graph={graph} path={['root']} onChange={updateGraph} /></> : <button type="button" onClick={() => updateGraph({ root: createExpressionNode('literal', expectedType) })}>Add literal</button>}
      {diagnostics.length > 0 ? <div role="alert" className="space-y-1 rounded bg-rose-50 p-2 text-xs text-rose-700">{diagnostics.map((diagnostic) => <p key={`${diagnostic.path}-${diagnostic.code}`}>{diagnostic.path}: {diagnostic.message}{diagnostic.suggestions?.length ? ` Suggested: ${diagnostic.suggestions.join(', ')}` : ''}</p>)}</div> : null}
      {preview.error ? <p role="alert" data-testid="expression-preview-error" className="rounded bg-rose-50 p-2 text-xs text-rose-700">Preview failed: {preview.error}</p> : preview.value !== undefined ? <p data-testid="expression-preview-result" className="rounded bg-emerald-50 p-2 text-xs text-emerald-700">Preview: {formatPreview(preview.value)}</p> : null}
      {expertError ? <p role="alert" className="rounded bg-rose-50 p-2 text-xs text-rose-700">{expertError}</p> : null}
    </div> : null}
  </div>;
}

function wrapNode(kind: ExpressionNode['kind'], root: ExpressionNode | null, valueType: DesignerValueType): ExpressionNode {
  const child = root ?? createExpressionNode('literal', valueType);
  if (kind === 'condition') return { kind, when: createExpressionNode('literal', 'boolean'), then: child, otherwise: createExpressionNode('literal', valueType), valueType };
  if (kind === 'logic') return { kind, operator: 'and', args: [createExpressionNode('literal', 'boolean'), createExpressionNode('literal', 'boolean')], valueType: 'boolean' };
  if (kind === 'conversion') return { kind, input: child, pipeline: [], valueType };
  if (kind === 'functionCall') return { kind, functionId: 'coalesce', args: [child], valueType };
  if (kind === 'defaultValue') return { kind, input: child, fallback: '', valueType };
  if (kind === 'object') return { kind, properties: {}, valueType: valueType === 'object' ? 'object' : 'json' };
  if (kind === 'array') return { kind, items: [], valueType: 'array' };
  if (kind === 'template') return { kind, items: [child], valueType: 'string' };
  return child;
}

function formatPreview(value: unknown): string { if (typeof value === 'string') return value; try { return JSON.stringify(value); } catch { return String(value); } }
