import { AlignCenterHorizontal, AlignCenterVertical, AlignEndHorizontal, AlignEndVertical, AlignHorizontalSpaceBetween, AlignStartHorizontal, AlignStartVertical, AlignVerticalSpaceBetween, ArrowDown, ArrowUp, BoxSelect, Grid3X3, LayoutList, MousePointer2, StretchHorizontal, StretchVertical } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';

import { createLayoutOperationCommand, createMoveNodesCommand, createSetLayoutModeCommand } from '../commands/createDesignerCommands';
import { DesignerCommandBus } from '../commands/DesignerCommandBus';
import type { DesignerDocument } from '../document/DesignerDocument';

import { isLayoutOperationSupported, resolveLayoutMode, type LayoutMode, type LayoutOperation } from './layoutOperations';

interface LayoutEditorToolbarProps {
  commandBus: DesignerCommandBus;
  document: DesignerDocument;
  selectedNodeIds: readonly string[];
  studioText?: (key: string) => string;
  onCommandResult?: (document: DesignerDocument) => void;
}

const modes: readonly LayoutMode[] = ['free', 'flex', 'grid', 'constraints'];
const operations: readonly LayoutOperation[] = ['align-left', 'align-center', 'align-right', 'align-top', 'align-middle', 'align-bottom', 'distribute-horizontal', 'distribute-vertical', 'same-width', 'same-height'];

export function LayoutEditorToolbar({ commandBus, document, selectedNodeIds, studioText = defaultLayoutText, onCommandResult }: LayoutEditorToolbarProps) {
  const containerId = useMemo(() => resolveContainerId(document, selectedNodeIds), [document, selectedNodeIds]);
  const container = containerId ? document.elements[containerId] : undefined;
  const mode = resolveLayoutMode(container?.layout ?? {});
  const [flexDirection, setFlexDirection] = useState<'row' | 'column'>(container?.layout.flexDirection === 'column' ? 'column' : 'row');
  const [columns, setColumns] = useState(() => readPositiveInteger(container?.layout.columns, 2));
  const [rows, setRows] = useState(() => readPositiveInteger(container?.layout.rows, 1));
  const [gap, setGap] = useState(() => readNonNegativeNumber(container?.layout.gap));

  useEffect(() => {
    setFlexDirection(container?.layout.flexDirection === 'column' ? 'column' : 'row');
    setColumns(readPositiveInteger(container?.layout.columns, 2));
    setRows(readPositiveInteger(container?.layout.rows, 1));
    setGap(readNonNegativeNumber(container?.layout.gap));
  }, [container?.id, container?.layout.columns, container?.layout.flexDirection, container?.layout.gap, container?.layout.rows]);

  const applyMode = (nextMode: LayoutMode) => {
    if (!containerId) return;
    const result = commandBus.execute(createSetLayoutModeCommand(containerId, { mode: nextMode, flexDirection, columns, gap, rows }, `layout:${containerId}`));
    if (result.changed) onCommandResult?.(result.document);
  };

  const applyLayoutValue = (patch: { columns?: number; flexDirection?: 'row' | 'column'; gap?: number; rows?: number }) => {
    if (!containerId) return;
    const nextFlexDirection = patch.flexDirection ?? flexDirection;
    const nextColumns = patch.columns ?? columns;
    const nextRows = patch.rows ?? rows;
    const nextGap = patch.gap ?? gap;
    if (patch.flexDirection) setFlexDirection(nextFlexDirection);
    if (patch.columns !== undefined) setColumns(nextColumns);
    if (patch.rows !== undefined) setRows(nextRows);
    if (patch.gap !== undefined) setGap(nextGap);
    const result = commandBus.execute(createSetLayoutModeCommand(containerId, { mode, flexDirection: nextFlexDirection, columns: nextColumns, gap: nextGap, rows: nextRows }, `layout:${containerId}`));
    if (result.changed) onCommandResult?.(result.document);
  };

  const applyOperation = (operation: LayoutOperation) => {
    if (selectedNodeIds.length === 0) return;
    const result = commandBus.execute(createLayoutOperationCommand(selectedNodeIds, operation, containerId ?? undefined));
    if (result.changed) onCommandResult?.(result.document);
  };

  const childOrder = resolveChildOrder(document, selectedNodeIds, mode);
  const moveSelectedChild = (delta: -1 | 1) => {
    if (!childOrder) return;
    const result = commandBus.execute(createMoveNodesCommand([childOrder.nodeId], childOrder.parentId, Math.max(0, childOrder.indexAfterRemoval + delta)));
    if (result.changed) onCommandResult?.(result.document);
  };

  return <section aria-label={studioText('layoutEditor')} className="page-studio__layout-toolbar" data-canvas-interaction-control="true">
    <span className="page-studio__layout-title">{studioText('layout')}</span>
    {!containerId ? <span className="page-studio__layout-empty">{studioText('selectContainerOrChild')}</span> : modes.map((nextMode) => <button aria-pressed={mode === nextMode} className={`page-studio__layout-mode ${mode === nextMode ? 'is-active' : ''}`} key={nextMode} title={modeLabel(nextMode, studioText)} type="button" onClick={() => applyMode(nextMode)}><ModeIcon mode={nextMode} /><span>{modeLabel(nextMode, studioText)}</span></button>)}
    {mode === 'flex' ? <label className="page-studio__layout-field"><span>{studioText('direction')}</span><select aria-label={studioText('flexDirection')} className="form-input h-7" value={flexDirection} onChange={(event) => applyLayoutValue({ flexDirection: event.target.value as 'row' | 'column' })}><option value="row">row</option><option value="column">column</option></select></label> : null}
    {mode === 'grid' ? <><label className="page-studio__layout-field"><span>{studioText('columns')}</span><input aria-label={studioText('gridColumns')} className="form-input h-7 w-14" min={1} type="number" value={columns} onChange={(event) => applyLayoutValue({ columns: readPositiveInteger(Number(event.target.value), 1) })} /></label><label className="page-studio__layout-field"><span>{studioText('rows')}</span><input aria-label={studioText('gridRows')} className="form-input h-7 w-14" min={1} type="number" value={rows} onChange={(event) => applyLayoutValue({ rows: readPositiveInteger(Number(event.target.value), 1) })} /></label></> : null}
    {mode === 'flex' || mode === 'grid' ? <label className="page-studio__layout-field"><span>{studioText('gap')}</span><input aria-label={studioText('layoutGap')} className="form-input h-7 w-14" min={0} type="number" value={gap} onChange={(event) => applyLayoutValue({ gap: readNonNegativeNumber(Number(event.target.value)) })} /></label> : null}
    {childOrder ? <div className="page-studio__layout-group" data-layout-reorder="true"><button aria-label={studioText('moveChildUp')} className="page-studio__layout-icon" disabled={!childOrder.canMoveUp} title={studioText('moveChildUp')} type="button" onClick={() => moveSelectedChild(-1)}><ArrowUp aria-hidden="true" className="h-3.5 w-3.5" /></button><button aria-label={studioText('moveChildDown')} className="page-studio__layout-icon" disabled={!childOrder.canMoveDown} title={studioText('moveChildDown')} type="button" onClick={() => moveSelectedChild(1)}><ArrowDown aria-hidden="true" className="h-3.5 w-3.5" /></button></div> : null}
    {selectedNodeIds.length > 1 ? <div className="page-studio__layout-group">{operations.map((operation) => { const supported = isLayoutOperationSupported(mode, operation); return <button aria-label={operationLabel(operation, studioText)} className="page-studio__layout-icon" data-layout-operation={operation} disabled={!supported} key={operation} title={operationLabel(operation, studioText)} type="button" onClick={() => applyOperation(operation)}><OperationIcon operation={operation} /></button>; })}</div> : null}
  </section>;
}

function resolveContainerId(document: DesignerDocument, selectedNodeIds: readonly string[]): string | null {
  const selected = selectedNodeIds.map((id) => document.elements[id]).filter(Boolean);
  const direct = selected.find((node) => node.children.length > 0);
  return direct?.id ?? selected[0]?.parentId ?? null;
}

function resolveChildOrder(document: DesignerDocument, selectedNodeIds: readonly string[], mode: LayoutMode): { canMoveDown: boolean; canMoveUp: boolean; indexAfterRemoval: number; nodeId: string; parentId: string } | null {
  if ((mode !== 'flex' && mode !== 'grid') || selectedNodeIds.length !== 1) return null;
  const node = document.elements[selectedNodeIds[0]];
  const parent = node?.parentId ? document.elements[node.parentId] : undefined;
  if (!node || !parent) return null;
  const index = parent.children.indexOf(node.id);
  if (index < 0) return null;
  const indexAfterRemoval = index;
  return { canMoveDown: index < parent.children.length - 1, canMoveUp: index > 0, indexAfterRemoval, nodeId: node.id, parentId: parent.id };
}

function modeLabel(mode: LayoutMode, text: (key: string) => string): string { return text(mode === 'free' ? 'freeLayout' : mode === 'constraints' ? 'constraintsLayout' : mode === 'flex' ? 'flexLayout' : 'gridLayout'); }
function operationLabel(operation: LayoutOperation, text: (key: string) => string): string { return text(({ 'align-left': 'alignLeft', 'align-center': 'alignCenter', 'align-right': 'alignRight', 'align-top': 'alignTop', 'align-middle': 'alignMiddle', 'align-bottom': 'alignBottom', 'distribute-horizontal': 'distributeHorizontal', 'distribute-vertical': 'distributeVertical', 'same-width': 'sameWidth', 'same-height': 'sameHeight' })[operation]); }

function ModeIcon({ mode }: { mode: LayoutMode }) {
  const props = { className: 'h-3.5 w-3.5 shrink-0' };
  switch (mode) {
    case 'free': return <MousePointer2 {...props} />;
    case 'flex': return <LayoutList {...props} />;
    case 'grid': return <Grid3X3 {...props} />;
    case 'constraints': return <BoxSelect {...props} />;
    default: return null;
  }
}

function OperationIcon({ operation }: { operation: LayoutOperation }) {
  const props = { className: 'h-3.5 w-3.5 shrink-0' };
  switch (operation) {
    case 'align-left': return <AlignStartVertical {...props} />;
    case 'align-center': return <AlignCenterVertical {...props} />;
    case 'align-right': return <AlignEndVertical {...props} />;
    case 'align-top': return <AlignStartHorizontal {...props} />;
    case 'align-middle': return <AlignCenterHorizontal {...props} />;
    case 'align-bottom': return <AlignEndHorizontal {...props} />;
    case 'distribute-horizontal': return <AlignHorizontalSpaceBetween {...props} />;
    case 'distribute-vertical': return <AlignVerticalSpaceBetween {...props} />;
    case 'same-width': return <StretchHorizontal {...props} />;
    case 'same-height': return <StretchVertical {...props} />;
    default: return null;
  }
}

function readPositiveInteger(value: unknown, fallback: number): number { return typeof value === 'number' && Number.isFinite(value) ? Math.max(1, Math.floor(value)) : fallback; }
function readNonNegativeNumber(value: unknown): number { return typeof value === 'number' && Number.isFinite(value) ? Math.max(0, value) : 0; }
function defaultLayoutText(key: string): string {
  const labels: Record<string, string> = {
    alignBottom: '底部对齐', alignCenter: '水平居中', alignLeft: '左对齐', alignMiddle: '垂直居中', alignRight: '右对齐', alignTop: '顶部对齐', columns: 'Columns', constraintsLayout: '约束布局', direction: 'Direction', distributeHorizontal: '水平分布', distributeVertical: '垂直分布', flexDirection: 'Flex direction', flexLayout: 'Flex', freeLayout: '自由布局', gap: 'Gap', gridColumns: 'Grid 列数', gridLayout: 'Grid', gridRows: 'Grid 行数', layout: 'Layout', layoutEditor: 'Layout editor toolbar', layoutGap: 'Layout gap', moveChildDown: 'Move child down', moveChildUp: 'Move child up', rows: 'Rows', sameHeight: '统一高度', sameWidth: '统一宽度', selectContainerOrChild: 'Select a container or child'
  };
  return labels[key] ?? key;
}
