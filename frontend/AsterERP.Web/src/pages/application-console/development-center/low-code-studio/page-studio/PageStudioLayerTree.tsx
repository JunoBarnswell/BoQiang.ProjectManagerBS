import { ChevronDown, ChevronRight, GripVertical, Layers3 } from 'lucide-react';
import { useEffect, useRef, useState, type DragEvent } from 'react';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import type { ComponentRegistry } from '../components/ComponentRegistry';
import type { DesignerDocument, DesignerDocumentNode } from '../document/DesignerDocument';

interface PageStudioLayerTreeProps {
  document: DesignerDocument;
  manifests: ComponentRegistry;
  selectedNodeIds: readonly string[];
  onMove: (nodeId: string, parentId: string, index: number) => void;
  onOpenPageSettings: () => void;
  onSelect: (nodeId: string) => void;
}

interface LayerDropTarget { index: number; parentId: string; placement: 'after' | 'before' | 'inside'; targetId: string }

export function PageStudioLayerTree({ document, manifests, selectedNodeIds, onMove, onOpenPageSettings, onSelect }: PageStudioLayerTreeProps) {
  const { translate } = useI18n();
  const [collapsed, setCollapsed] = useState<ReadonlySet<string>>(new Set());
  const [dropTarget, setDropTarget] = useState<LayerDropTarget | null>(null);
  const treeRef = useRef<HTMLDivElement>(null);
  const rootIds = new Set(document.pages.map((page) => page.rootElementId));
  const toggle = (nodeId: string) => setCollapsed((current) => current.has(nodeId) ? new Set([...current].filter((id) => id !== nodeId)) : new Set([...current, nodeId]));

  useEffect(() => {
    if (selectedNodeIds.length === 0) return;
    const firstId = selectedNodeIds[0];
    const parents = new Set<string>();
    let currentId = firstId;
    while (currentId) {
      const node = document.elements[currentId];
      if (!node?.parentId) break;
      parents.add(node.parentId);
      currentId = node.parentId;
    }
    if (parents.size > 0) setCollapsed((current) => {
      const next = new Set(current);
      for (const parent of parents) next.delete(parent);
      return next.size === current.size ? current : next;
    });
    const timer = window.setTimeout(() => {
      const container = treeRef.current;
      const element = container?.querySelector<HTMLElement>(`[data-layer-node-id="${firstId}"]`);
      if (!container || !element) return;
      const containerRect = container.getBoundingClientRect();
      const elementRect = element.getBoundingClientRect();
      if (elementRect.top < containerRect.top) container.scrollTop -= containerRect.top - elementRect.top;
      else if (elementRect.bottom > containerRect.bottom) container.scrollTop += elementRect.bottom - containerRect.bottom;
    }, 50);
    return () => window.clearTimeout(timer);
  }, [selectedNodeIds, document.elements]);

  const resolveDropTarget = (draggedId: string, target: DesignerDocumentNode, event: DragEvent<HTMLElement>): LayerDropTarget | null => {
    const dragged = document.elements[draggedId];
    if (!dragged || dragged.locked || rootIds.has(draggedId) || target.locked) return null;
    if (target.id === draggedId || isDescendant(document.elements, draggedId, target.id)) return null;
    const acceptsChildren = manifests.get(target.type)?.capability.acceptsChildren === true;
    if (acceptsChildren) return { index: target.children.filter((childId) => childId !== draggedId).length, parentId: target.id, placement: 'inside', targetId: target.id };
    const parentId = target.parentId;
    const parent = parentId ? document.elements[parentId] : undefined;
    if (!parentId || !parent || parent.locked) return null;
    const remainingChildren = parent.children.filter((childId) => childId !== draggedId);
    const index = remainingChildren.indexOf(target.id);
    if (index < 0) return null;
    const before = event.clientY < event.currentTarget.getBoundingClientRect().top + event.currentTarget.getBoundingClientRect().height / 2;
    return { index: index + (before ? 0 : 1), parentId, placement: before ? 'before' : 'after', targetId: target.id };
  };

  const renderNode = (node: DesignerDocumentNode, level: number) => {
    const children = node.children.map((id) => document.elements[id]).filter((child): child is DesignerDocumentNode => Boolean(child));
    const expanded = children.length > 0 && !collapsed.has(node.id);
    const root = rootIds.has(node.id);
    const selected = !root && selectedNodeIds.includes(node.id);
    const indicator = dropTarget?.targetId === node.id ? dropTarget.placement : null;
    return <div key={node.id}>
      <div aria-level={level} aria-selected={root ? undefined : selected} className={`page-studio__layer-row ${root ? 'is-root' : ''} ${selected ? 'is-selected' : ''} ${node.locked ? 'is-locked' : ''}`} data-layer-node-id={node.id} data-layer-drop={indicator ?? undefined} draggable={!root && !node.locked} role="treeitem" tabIndex={0} onClick={() => root ? onOpenPageSettings() : onSelect(node.id)} onDragEnd={() => setDropTarget(null)} onDragLeave={() => setDropTarget((current) => current?.targetId === node.id ? null : current)} onDragOver={(event) => { const draggedId = event.dataTransfer.getData('application/x-astererp-designer-node'); const target = resolveDropTarget(draggedId, node, event); if (!target) return; event.preventDefault(); event.dataTransfer.dropEffect = 'move'; setDropTarget(target); }} onDragStart={(event) => { event.dataTransfer.effectAllowed = 'move'; event.dataTransfer.setData('application/x-astererp-designer-node', node.id); }} onDrop={(event) => { event.preventDefault(); const draggedId = event.dataTransfer.getData('application/x-astererp-designer-node'); const target = resolveDropTarget(draggedId, node, event); setDropTarget(null); if (target) onMove(draggedId, target.parentId, target.index); }} onKeyDown={(event) => { if (event.key !== 'Enter' && event.key !== ' ') return; event.preventDefault(); if (root) onOpenPageSettings(); else onSelect(node.id); }}>
        {indicator ? <span className={`page-studio__layer-drop page-studio__layer-drop--${indicator}`} /> : null}
        {!root ? <GripVertical aria-hidden="true" className="page-studio__layer-grip" /> : <span className="page-studio__layer-spacer" />}
        <button aria-label={`${translate(expanded ? 'lowCode.pageStudio.collapse' : 'lowCode.pageStudio.expand')} ${node.name ?? node.type}`} className="page-studio__layer-toggle" type="button" onClick={(event) => { event.stopPropagation(); toggle(node.id); }}>{children.length > 0 ? (expanded ? <ChevronDown aria-hidden="true" className="h-3 w-3" /> : <ChevronRight aria-hidden="true" className="h-3 w-3" />) : <span className="page-studio__layer-spacer" />}</button><Layers3 aria-hidden="true" className="page-studio__layer-icon" /><span className="page-studio__layer-name">{root ? translate('lowCode.pageStudio.artboard') : node.name ?? node.type}</span><span className="page-studio__layer-type" title={node.type}>{root ? translate('lowCode.pageStudio.page') : node.type}</span>
      </div>
      {expanded ? <div className="page-studio__layer-children">{children.map((child) => renderNode(child, level + 1))}</div> : null}
    </div>;
  };
  return <section aria-label={translate('lowCode.pageStudio.layers')} className="page-studio__layers"><p>{translate('lowCode.pageStudio.layerTreeHelp')}</p><div ref={treeRef} aria-label={translate('lowCode.pageStudio.layerTree')} className="page-studio__layer-tree" role="tree">{document.pages.map((page) => document.elements[page.rootElementId]).filter((node): node is DesignerDocumentNode => Boolean(node)).map((node) => renderNode(node, 1))}</div></section>;
}

function isDescendant(elements: Readonly<Record<string, DesignerDocumentNode>>, ancestorId: string, candidateId: string): boolean {
  const visited = new Set<string>();
  let currentId = candidateId;
  while (currentId && !visited.has(currentId)) {
    if (currentId === ancestorId) return true;
    visited.add(currentId);
    currentId = elements[currentId]?.parentId ?? '';
  }
  return false;
}
