import { useMemo } from 'react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { createPatchResponsiveOverrideCommand } from '../commands/createDesignerCommands';
import type { DesignerCommandBus } from '../commands/DesignerCommandBus';
import type { DesignerDocument, DesignerDocumentNode } from '../document/DesignerDocument';

import { createResponsivePropertyDiff, createResponsivePropertyResetPatch, type ResponsiveBreakpoint, type ResponsiveNode } from './responsiveModel';

interface ResponsiveOverrideDiffPanelProps {
  breakpoints: readonly ResponsiveBreakpoint[];
  commandBus?: DesignerCommandBus;
  document: DesignerDocument;
  nodeIds: readonly string[];
  onDocumentChange?: (document: DesignerDocument) => void;
  selectedBreakpoint: ResponsiveBreakpoint | null;
}

export function ResponsiveOverrideDiffPanel({ breakpoints, commandBus, document, nodeIds, onDocumentChange, selectedBreakpoint }: ResponsiveOverrideDiffPanelProps) {
  const { translate } = useI18n();
  if (!selectedBreakpoint || nodeIds.length === 0) return null;
  return <section aria-label={translate('lowCode.pageStudio.responsiveOverrides')} className="page-studio__responsive-diff">
    <div className="page-studio__responsive-diff-header"><div><p>{translate('lowCode.pageStudio.responsiveOverrides')}</p><h3>{selectedBreakpoint.label ?? selectedBreakpoint.id}</h3></div><span>{translate('lowCode.pageStudio.inherited')} / {translate('lowCode.pageStudio.current')} / {translate('lowCode.pageStudio.source')}</span></div>
    <div className="page-studio__responsive-diff-list">{nodeIds.map((nodeId) => <NodeDiff key={nodeId} breakpoints={breakpoints} commandBus={commandBus} document={document} nodeId={nodeId} onDocumentChange={onDocumentChange} selectedBreakpoint={selectedBreakpoint} />)}</div>
  </section>;
}

function NodeDiff({ breakpoints, commandBus, document, nodeId, onDocumentChange, selectedBreakpoint }: Omit<ResponsiveOverrideDiffPanelProps, 'nodeIds' | 'selectedBreakpoint'> & { nodeId: string; selectedBreakpoint: ResponsiveBreakpoint }) {
  const { translate } = useI18n();
  const node = document.elements[nodeId];
  const diffs = useMemo(() => node ? createResponsivePropertyDiff(toResponsiveNode(node), selectedBreakpoint, breakpoints) : [], [breakpoints, node, selectedBreakpoint]);
  if (!node) return null;
  return <div className="page-studio__responsive-node"><p className="page-studio__responsive-node-name">{node.name ?? node.id}</p>{diffs.length === 0 ? <p className="page-studio__responsive-empty">{translate('lowCode.pageStudio.noResponsiveDifferences')}</p> : <div className="page-studio__responsive-rows">{diffs.map((diff) => <DiffRow key={diff.path} diff={diff} nodeId={nodeId} commandBus={commandBus} onDocumentChange={onDocumentChange} selectedBreakpoint={selectedBreakpoint} />)}</div>}</div>;
}

function DiffRow({ commandBus, diff, nodeId, onDocumentChange, selectedBreakpoint }: { commandBus?: DesignerCommandBus; diff: ReturnType<typeof createResponsivePropertyDiff>[number]; nodeId: string; onDocumentChange?: (document: DesignerDocument) => void; selectedBreakpoint: ResponsiveBreakpoint }) {
  const { translate } = useI18n();
  const reset = () => {
    if (!commandBus || !diff.hasOverride) return;
    const result = commandBus.execute(createPatchResponsiveOverrideCommand(nodeId, selectedBreakpoint.id, createResponsivePropertyResetPatch(diff.path)));
    if (result.changed) onDocumentChange?.(result.document);
  };
  return <div className="page-studio__responsive-row" data-responsive-diff-path={diff.path}><div className="page-studio__responsive-row-header"><code>{diff.path}</code>{diff.hasOverride ? <button disabled={!commandBus} type="button" onClick={reset}>{translate('lowCode.pageStudio.resetToInherited')}</button> : null}</div><div className="page-studio__responsive-values"><span>{translate('lowCode.pageStudio.inherited')}</span><span>{formatValue(diff.inheritedValue)}</span><span>{translate('lowCode.pageStudio.current')}</span><span className="is-current">{formatValue(diff.currentValue)}</span><span>{translate('lowCode.pageStudio.source')}</span><span>{diff.sourceBreakpointId ?? translate('lowCode.pageStudio.base')}</span></div></div>;
}

function toResponsiveNode(node: DesignerDocumentNode): ResponsiveNode {
  return { base: { layout: node.layout, props: node.props, style: node.style ?? {} }, responsiveOverrides: node.responsiveOverrides ?? {} };
}

function formatValue(value: unknown): string {
  if (value === undefined) return '—';
  if (typeof value === 'string') return value;
  const serialized = JSON.stringify(value);
  return serialized ?? String(value);
}
