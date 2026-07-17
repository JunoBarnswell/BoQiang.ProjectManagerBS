import type { DesignerCommand, DesignerCommandResult } from '../commands/DesignerCommand';
import { createInverseDesignerCommand } from '../commands/DesignerDocumentPatch';
import type { DesignerDocument, DesignerDocumentNode } from '../document/DesignerDocument';

import { cloneClipboardForest, type ClipboardTree } from './clipboardTree';
import { readId, resourceKeyFor, rewriteClipboardValue, type DesignerClipboardPayload } from './designerClipboardPayload';

export function createPasteClipboardCommand(payload: DesignerClipboardPayload, parentId: string, idFactory: (sourceId: string, occupied: ReadonlySet<string>) => string): DesignerCommand {
  return {
    id: 'PasteClipboard', label: 'Paste clipboard subtree', execute: ({ document }) => {
      const parent = document.elements[parentId];
      if (!parent) return failure(document, [`Parent not found: ${parentId}`]);
      const occupied = new Set(Object.keys(document.elements));
      const resourceRemap = new Map<string, string>();
      const actionRemap = new Map<string, string>();
      const nextActions = mergeRecords(document.actions, payload.actions, actionRemap, null, idFactory, true);
      const nextApiBindings = mergeRecords(document.apiBindings, payload.apiBindings, resourceRemap, 'api', idFactory, false);
      const nextDataSources = mergeRecords(document.dataSources, payload.dataSources, resourceRemap, 'dataSource', idFactory, false);
      const nextPageParameters = mergeRecords(document.pageParameters, payload.pageParameters, resourceRemap, 'page', idFactory, false);
      const nextVariables = mergeRecords(document.variables, payload.variables, resourceRemap, 'variables', idFactory, false);
      const nextWorkflowBindings = mergeRecords(document.workflowBindings, payload.workflowBindings, resourceRemap, 'workflow', idFactory, false);
      const pasted = cloneClipboardForest(payload.trees, parentId, (sourceId) => { const id = idFactory(sourceId, occupied); occupied.add(id); return id; }, {
        create: (source, id, nextParentId, children) => {
          const rewritten = rewriteClipboardValue(source, resourceRemap, actionRemap) as DesignerDocumentNode;
          return { ...rewritten, id, parentId: nextParentId, children: [...children] };
        }
      });
      const elements = { ...document.elements };
      const roots = pasted.map((tree) => tree.root.id);
      for (const tree of pasted) flatten(tree).forEach((node) => { elements[node.id] = node; });
      elements[parentId] = { ...parent, children: [...parent.children, ...roots] };
      const diagnostics = payload.missingResources.map((resourceId) => `warning: External resource unresolved after paste: ${resourceId}`);
      const next = { ...document, actions: nextActions, apiBindings: nextApiBindings, dataSources: nextDataSources, pageParameters: nextPageParameters, variables: nextVariables, workflowBindings: nextWorkflowBindings, elements };
      return { changed: true, diagnostics, document: next, inverse: createInverseDesignerCommand(document, next, 'PasteClipboard', 'paste clipboard subtree') };
    }
  };
}

function flatten(tree: ClipboardTree<DesignerDocumentNode>): DesignerDocumentNode[] { return [tree.root, ...tree.children.flatMap(flatten)]; }
function failure(document: DesignerDocument, diagnostics: string[]): DesignerCommandResult { return { changed: false, diagnostics, document }; }

function mergeRecords(target: readonly Record<string, unknown>[], incoming: readonly Record<string, unknown>[], remap: Map<string, string>, prefix: 'api' | 'dataSource' | 'page' | 'variables' | 'workflow' | null, idFactory: (sourceId: string, occupied: ReadonlySet<string>) => string, alwaysRemap: boolean): Record<string, unknown>[] {
  const result = target.map((item) => structuredClone(item));
  const occupied = new Set(result.map(readId).filter(Boolean));
  for (const source of incoming) {
    const sourceId = readId(source);
    if (!sourceId) continue;
    let targetId = sourceId;
    if (alwaysRemap || occupied.has(targetId)) {
      targetId = idFactory(sourceId, occupied);
      while (occupied.has(targetId)) targetId = idFactory(targetId, occupied);
    }
    occupied.add(targetId);
    if (prefix) remap.set(resourceKeyFor(prefix === 'api' ? 'apiBindings' : prefix === 'dataSource' ? 'dataSources' : prefix === 'page' ? 'pageParameters' : prefix, sourceId), resourceKeyFor(prefix === 'api' ? 'apiBindings' : prefix === 'dataSource' ? 'dataSources' : prefix === 'page' ? 'pageParameters' : prefix, targetId));
    else remap.set(sourceId, targetId);
    if (!result.some((item) => readId(item) === targetId)) result.push({ ...rewriteClipboardValue(source, remap, remap) as Record<string, unknown>, id: targetId });
  }
  return result;
}
