import { Search, X } from 'lucide-react';
import { useEffect, useMemo, useRef, useState, type PointerEvent as ReactPointerEvent } from 'react';
import { createPortal } from 'react-dom';

import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { PermissionButton } from '../../../../../shared/auth/PermissionButton';
import { elementsAtPoint } from '../canvas/canvasHitTesting';
import { createInsertNodesCommand } from '../commands/createDesignerCommands';
import type { DesignerCommandResult } from '../commands/DesignerCommand';
import { DesignerCommandBus } from '../commands/DesignerCommandBus';
import type { DesignerDocument } from '../document/DesignerDocument';
import { resolveLayoutMode } from '../layout/layoutOperations';

import { filterComponentCatalog } from './componentCatalogModel';
import { getComponentIcon } from './componentIcons';
import { createComponentNode, createComponentNodeId, createComponentPointerDragEndEvent, createComponentPointerDragEvent, createComponentPointerDropEvent, type ComponentInsertionPosition } from './componentInsertion';
import type { ComponentManifest } from './ComponentManifest';
import { ComponentRegistry } from './ComponentRegistry';

interface ComponentInsertPaletteProps {
  commandBus: DesignerCommandBus;
  document: DesignerDocument;
  manifests: ComponentRegistry;
  targetParentId: string | null;
  onCommandResult?: (result: DesignerCommandResult) => void;
  onPointerDragStart?: () => void;
  onNodeInserted?: (nodeId: string) => void;
}

export function ComponentInsertPalette({ commandBus, document, manifests, targetParentId, onCommandResult, onNodeInserted, onPointerDragStart }: ComponentInsertPaletteProps) {
  const { translate } = useI18n();
  const [query, setQuery] = useState('');
  const entries = useMemo(() => filterComponentCatalog(manifests.values(), query), [manifests, query]);
  const groupedEntries = useMemo(() => {
    const groups = new Map<string, ComponentManifest[]>();
    for (const manifest of entries) {
      const category = manifest.type.split('.')[0] || 'other';
      if (!groups.has(category)) groups.set(category, []);
      groups.get(category)!.push(manifest);
    }
    return Array.from(groups.entries()).sort((a, b) => a[0].localeCompare(b[0]));
  }, [entries]);
  const suppressNextClickRef = useRef(false);
  const dragGhostRef = useRef<HTMLDivElement | null>(null);
  const dragPositionRef = useRef({ x: 0, y: 0 });
  const [dragPreview, setDragPreview] = useState<ComponentManifest | null>(null);
  useEffect(() => {
    if (!dragGhostRef.current) return;
    dragGhostRef.current.style.transform = `translate3d(${dragPositionRef.current.x}px, ${dragPositionRef.current.y}px, 0)`;
  }, [dragPreview]);
  const insert = (manifest: ComponentManifest) => {
    if (!targetParentId || !document.elements[targetParentId]) return;
    const parentNode = document.elements[targetParentId];
    const parentMode = resolveLayoutMode(parentNode.layout ?? {});
    let position: ComponentInsertionPosition | undefined = undefined;
    if (parentMode === 'free' || parentMode === 'constraints') {
      const children = parentNode.children.map(id => document.elements[id]).filter(Boolean);
      const maxY = children.reduce((max, child) => {
        const y = typeof child.layout.y === 'number' ? child.layout.y : 0;
        const height = typeof child.layout.height === 'number' ? child.layout.height : 0;
        return Math.max(max, y + height);
      }, 0);
      position = { x: 16, y: maxY > 0 ? maxY + 16 : 16 };
    }
    const node = createComponentNode(manifest, targetParentId, createComponentNodeId(manifest.type, new Set(Object.keys(document.elements))), position);
    const result = commandBus.execute(createInsertNodesCommand([node]));
    onCommandResult?.(result);
    if (result.changed) onNodeInserted?.(node.id);
  };
  const handlePointerDown = (event: ReactPointerEvent<HTMLButtonElement>, manifest: ComponentManifest) => {
    if (!targetParentId) return;
    const startX = event.clientX;
    const startY = event.clientY;
    let dragging = false;
    let cleaned = false;
    const handlePointerUp = (upEvent: PointerEvent) => {
      if (upEvent.pointerId !== event.pointerId) return;
      cleanup();
      const moved = Math.hypot(upEvent.clientX - startX, upEvent.clientY - startY) >= 4;
      const targets = elementsAtPoint(window.document, upEvent.clientX, upEvent.clientY);
      if (!moved || targets.every((target) => Boolean(target.closest('[data-component-palette="true"]')))) return;
      suppressNextClickRef.current = true;
      window.dispatchEvent(createComponentPointerDropEvent({ clientX: upEvent.clientX, clientY: upEvent.clientY, pointerId: upEvent.pointerId, type: manifest.type }));
    };
    const handlePointerMove = (moveEvent: PointerEvent) => {
      if (moveEvent.pointerId !== event.pointerId || Math.hypot(moveEvent.clientX - startX, moveEvent.clientY - startY) < 4) return;
      if (!dragging) {
        dragging = true;
        onPointerDragStart?.();
        dragPositionRef.current = { x: moveEvent.clientX + 16, y: moveEvent.clientY + 16 };
        setDragPreview(manifest);
      } else {
        dragPositionRef.current = { x: moveEvent.clientX + 16, y: moveEvent.clientY + 16 };
        if (dragGhostRef.current) dragGhostRef.current.style.transform = `translate3d(${dragPositionRef.current.x}px, ${dragPositionRef.current.y}px, 0)`;
      }
      window.dispatchEvent(createComponentPointerDragEvent({ clientX: moveEvent.clientX, clientY: moveEvent.clientY, pointerId: moveEvent.pointerId, type: manifest.type }));
    };
    const handlePointerCancel = (cancelEvent: PointerEvent) => {
      if (cancelEvent.pointerId === event.pointerId) cleanup();
    };
    const cleanup = () => {
      if (cleaned) return;
      cleaned = true;
      if (dragging) window.dispatchEvent(createComponentPointerDragEndEvent(event.pointerId));
      setDragPreview(null);
      window.removeEventListener('pointerup', handlePointerUp);
      window.removeEventListener('pointermove', handlePointerMove);
      window.removeEventListener('pointercancel', handlePointerCancel);
      window.removeEventListener('lostpointercapture', handlePointerCancel);
    };
    window.addEventListener('pointerup', handlePointerUp);
    window.addEventListener('pointermove', handlePointerMove);
    window.addEventListener('pointercancel', handlePointerCancel);
    window.addEventListener('lostpointercapture', handlePointerCancel);
  };

  return <><section aria-label={translate('lowCode.pageStudio.componentPalette')} className="page-studio__palette" data-component-palette="true">
    <div className="page-studio__palette-header"><div className="min-w-0"><h2>{translate('lowCode.pageStudio.components')}</h2><p>{targetParentId ? translate('lowCode.pageStudio.readyToInsert') : translate('lowCode.pageStudio.selectContainerFirst')}</p></div><span className="page-studio__palette-count">{entries.length}</span></div>
    <label className="page-studio__palette-search"><Search aria-hidden="true" className="h-3.5 w-3.5" /><input aria-label={translate('lowCode.pageStudio.searchComponents')} placeholder={translate('lowCode.pageStudio.searchComponentsPlaceholder')} value={query} onChange={(event) => setQuery(event.target.value)} />{query ? <button aria-label={translate('lowCode.pageStudio.clearSearch')} type="button" onClick={() => setQuery('')}><X aria-hidden="true" className="h-3.5 w-3.5" /></button> : null}</label>
    <div className="page-studio__palette-list" role="list">
      {groupedEntries.map(([category, groupManifests]) => (
        <div className="page-studio__palette-group" key={category}>
          <h3>{category.charAt(0).toUpperCase() + category.slice(1)}</h3>
          <div className="page-studio__palette-grid">
            {groupManifests.map((manifest) => {
              const label = translate(manifest.i18n.labelKey);
              return <PermissionButton aria-label={label} className="page-studio__palette-item" code="app:development-center:designer:edit" disabled={!targetParentId} fallback="disable" iconStart={false} key={manifest.type} type="button" onClick={() => { if (suppressNextClickRef.current) { suppressNextClickRef.current = false; return; } insert(manifest); }} onPointerDown={(event) => handlePointerDown(event, manifest)}><span className={`page-studio__palette-icon ${manifest.capability.acceptsChildren ? 'is-container' : ''}`}>{getComponentIcon(manifest.type, manifest.capability.acceptsChildren)}</span><span className="page-studio__palette-label" title={`${label} (${manifest.type})`}>{label}</span></PermissionButton>;
            })}
          </div>
        </div>
      ))}
      {entries.length === 0 ? <p className="page-studio__palette-empty">{translate('lowCode.pageStudio.noMatchingComponents')}</p> : null}
    </div>
  </section>{dragPreview && typeof globalThis.document !== 'undefined' ? createPortal(<div ref={dragGhostRef} aria-hidden="true" className="page-studio__drag-preview"><span className="page-studio__drag-preview-icon">{getComponentIcon(dragPreview.type, dragPreview.capability.acceptsChildren)}</span><span>{translate(dragPreview.i18n.labelKey)}</span></div>, globalThis.document.body) : null}</>;
}
