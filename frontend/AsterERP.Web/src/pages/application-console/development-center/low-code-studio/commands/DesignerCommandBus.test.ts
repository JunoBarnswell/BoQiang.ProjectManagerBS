import { describe, expect, it } from 'vitest';

import { LayoutResolver } from '../../../../../runtime-kernel/LayoutResolver';
import type { DesignerDocument } from '../document/DesignerDocument';
import { computeDesignerDocumentHash } from '../document/DesignerDocumentHash';

import {
  createBatchPatchCommand,
  createBindValueCommand,
  createDeleteNodesCommand,
  createDuplicateSubtreeCommand,
  createInsertNodesCommand,
  createMoveNodesCommand,
  createPatchNodeCommand,
  createPatchResponsiveOverrideCommand
} from './createDesignerCommands';
import type { DesignerCommand } from './DesignerCommand';
import { DesignerCommandBus } from './DesignerCommandBus';
import { createDesignerDocumentPatch, createDesignerDocumentPatchCommand, invertDesignerDocumentPatch, type DesignerPatchCommand } from './DesignerDocumentPatch';

const document: DesignerDocument = {
  actions: [],
  apiBindings: [],
  dataSources: [],
  documentId: 'page-orders',
  elements: {
    root: { bindings: {}, children: ['child'], events: [], id: 'root', layout: {}, parentId: null, props: {}, type: 'layout.page' },
    child: { bindings: {}, children: [], events: [], id: 'child', layout: {}, parentId: 'root', props: {}, type: 'text.paragraph' }
  },
  metadata: {},
  modals: [],
  pageParameters: [],
  pages: [{ id: 'orders', name: 'Orders', rootElementId: 'root' }],
  pageType: 'standard',
  permissions: {},
  revision: 1,
  runtimeContext: {},
  styleTokens: {},
  variables: [],
  workflowBindings: []
};

describe('DesignerCommandBus', () => {
  it('executes, undoes and redoes node commands', () => {
    const bus = new DesignerCommandBus(document);
    const result = bus.execute(createPatchNodeCommand('child', { type: 'text.heading' }));
    expect((result.inverse as DesignerPatchCommand).patch.nodeChanges).toHaveLength(1);
    expect((result.inverse as DesignerPatchCommand).patch).not.toHaveProperty('document');
    expect(bus.document.elements.child.type).toBe('text.heading');
    bus.undo();
    expect(bus.document.elements.child.type).toBe('text.paragraph');
    bus.redo();
    expect(bus.document.elements.child.type).toBe('text.heading');
  });

  it('rejects invalid batches without changing the document', () => {
    const bus = new DesignerCommandBus(document);
    const result = bus.execute(createBatchPatchCommand({ missing: { type: 'x' } }));
    expect(result.changed).toBe(false);
    expect(bus.document.revision).toBe(1);
  });

  it('duplicates a complete subtree with new ids', () => {
    const bus = new DesignerCommandBus(document);
    bus.execute(createDuplicateSubtreeCommand('root', 'copy'));
    expect(bus.document.elements.copy.children).toEqual(['copy_1']);
    expect(bus.document.elements.copy_1.parentId).toBe('copy');
    expect(bus.document.elements.root.children).toContain('copy');
  });

  it('keeps parent children symmetric across move, duplicate, delete and inverse operations', () => {
    const bus = new DesignerCommandBus(nestedDocument(5));
    expect(bus.execute(createMoveNodesCommand(['level-2'], 'level-4')).diagnostics).toContain('Move would create a cycle: level-2 -> level-4');
    expect(bus.execute(createMoveNodesCommand(['level-2'], 'root')).changed).toBe(true);
    expect(bus.document.elements['level-2'].parentId).toBe('root');
    expect(bus.document.elements.root.children).toContain('level-2');
    expect(bus.document.elements['level-1'].children).not.toContain('level-2');
    expect(bus.execute(createDuplicateSubtreeCommand('level-2', 'copy-level-2')).changed).toBe(true);
    expect(bus.document.elements.root.children).toContain('copy-level-2');
    expect(bus.execute(createDeleteNodesCommand(['copy-level-2'])).changed).toBe(true);
    expect(bus.document.elements.root.children).not.toContain('copy-level-2');
    expect(bus.undo()?.changed).toBe(true);
    expect(bus.document.elements.root.children).toContain('copy-level-2');
  });

  it('rejects an invalid document at commit and leaves the prior snapshot unchanged', () => {
    const bus = new DesignerCommandBus(document);
    const result = bus.execute(createPatchNodeCommand('child', { parentId: null }));
    expect(result.changed).toBe(false);
    expect(result.diagnostics.length).toBeGreaterThan(0);
    expect(bus.document.elements.child.parentId).toBe('root');
    expect(bus.document.revision).toBe(1);
  });

  it('emits non-blocking monitoring events for accepted and rejected commands', () => {
    const events: unknown[] = [];
    const bus = new DesignerCommandBus(document, {
      monitoringContext: { tenantId: 'tenant-a', appCode: 'MES' },
      onMonitoringEvent: (event) => events.push(event)
    });

    bus.execute(createPatchNodeCommand('child', { type: 'text.heading' }));
    bus.execute(createDuplicateSubtreeCommand('root', 'child'));

    expect(events).toHaveLength(2);
    expect(events[0]).toMatchObject({
      context: { appCode: 'MES', commandId: 'PatchNode', commandType: 'PatchNode', tenantId: 'tenant-a' },
      eventName: 'designer.command',
      outcome: 'succeeded'
    });
    expect(events[1]).toMatchObject({
      context: { commandId: 'DuplicateSubtree', commandType: 'DuplicateSubtree' },
      errorCode: 'Source or destination node is invalid',
      eventName: 'designer.command.failed',
      outcome: 'failed'
    });
  });

  it('does not create history for a command with no canonical content change', () => {
    const bus = new DesignerCommandBus(document);
    const result = bus.execute(createMoveNodesCommand(['child'], 'root'));
    expect(result).toMatchObject({ changed: false, diagnostics: [] });
    expect(bus.document.revision).toBe(1);
    expect(bus.undo()).toBeNull();
  });

  it('commits reparenting, order and target-local layout as one reversible move', () => {
    const source = structuredClone(document);
    source.elements.root.children = ['child', 'right'];
    source.elements.root.layout = { layoutMode: 'free' };
    source.elements.child.layout = { height: 40, position: 'absolute', width: 80, x: 12, y: 16 };
    source.elements.right = { bindings: {}, children: [], events: [], id: 'right', layout: { height: 200, layoutMode: 'free', position: 'absolute', width: 200, x: 200, y: 0 }, parentId: 'root', props: {}, type: 'layout.container' };
    const bus = new DesignerCommandBus(source);

    const result = bus.execute(createMoveNodesCommand({
      insertionIndex: 0,
      layoutPatches: { child: { position: 'absolute', x: 24, y: 32 } },
      nodeIds: ['child'],
      parentId: 'right',
      targetLayoutMode: 'free'
    }));

    expect(result.changed).toBe(true);
    expect(bus.document.revision).toBe(2);
    expect(bus.document.elements.child).toMatchObject({ parentId: 'right', layout: { position: 'absolute', x: 24, y: 32 } });
    expect(bus.document.elements.root.children).toEqual(['right']);
    expect(bus.document.elements.right.children).toEqual(['child']);
    expect(bus.undo()?.changed).toBe(true);
    expect(bus.document.elements.child).toMatchObject({ parentId: 'root', layout: { position: 'absolute', x: 12, y: 16 } });
    expect(bus.undo()).toBeNull();
  });

  it('rejects duplicate IDs without mutating the source subtree or history', () => {
    const bus = new DesignerCommandBus(document);
    const result = bus.execute(createDuplicateSubtreeCommand('root', 'child'));
    expect(result.changed).toBe(false);
    expect(result.diagnostics).toEqual(['Source or destination node is invalid']);
    expect(bus.document.elements.root.children).toEqual(['child']);
    expect(bus.undo()).toBeNull();
  });

  it('restores the canonical document after every reversible command', () => {
    const resource = { conversionPipeline: [], displayName: 'Order status', expectedType: 'string' as const, resourceId: 'dataset.orders.status', resourceType: 'dataset', valueType: 'string' as const };
    const commands = [
      createPatchNodeCommand('child', { type: 'text.heading' }),
      createPatchResponsiveOverrideCommand('child', 'tablet', { layout: { minWidth: 480 } }),
      createBindValueCommand('child', 'value', resource),
      createBatchPatchCommand({ child: { props: { title: 'Orders' } } }),
      createInsertNodesCommand([{ children: [], events: [], id: 'inserted', layout: {}, parentId: 'root', props: {}, type: 'text.paragraph' }]),
      createDeleteNodesCommand(['child']),
      createDuplicateSubtreeCommand('root', 'copy')
    ];

    for (const command of commands) {
      const bus = new DesignerCommandBus(document);
      const before = JSON.stringify(contentOf(bus.document));
      expect(bus.execute(command).changed).toBe(true);
      expect(bus.undo()?.changed).toBe(true);
      expect(JSON.stringify(contentOf(bus.document))).toBe(before);
      expect(bus.document.revision).toBe(3);
      expect(bus.document.documentHash).toMatch(/^sha256:[0-9a-f]{64}$/);
    }
  });

  it('writes BindValue resources to props and nested binding slots through one reversible command', () => {
    const resource = { conversionPipeline: [], displayName: 'Order status', expectedType: 'string' as const, resourceId: 'variables:order.status', resourceType: 'variables', valueType: 'string' as const };
    const bus = new DesignerCommandBus(document);

    expect(bus.execute(createBindValueCommand('child', 'props.text', resource)).changed).toBe(true);
    expect(bus.document.elements.child.props.text).toEqual(resource);
    expect(bus.undo()?.changed).toBe(true);
    expect(bus.document.elements.child.props.text).toBeUndefined();

    expect(bus.execute(createBindValueCommand('child', 'bindings.data.field', resource)).changed).toBe(true);
    expect(bus.document.elements.child.bindings?.data?.field).toEqual(resource);
    expect(bus.undo()?.changed).toBe(true);
    expect(bus.document.elements.child.bindings?.data?.field).toBeUndefined();
  });

  it('keeps command history independent from document snapshots', () => {
    const bus = new DesignerCommandBus(document);
    bus.execute(createPatchNodeCommand('child', { type: 'text.heading' }));
    bus.execute(createBatchPatchCommand({ child: { props: { title: 'Orders' } } }));
    expect(bus.document.revision).toBe(3);
    bus.undo();
    expect(bus.document.elements.child.type).toBe('text.heading');
    expect(bus.document.elements.child.props.title).toBeUndefined();
    bus.undo();
    expect(bus.document.elements.child.type).toBe('text.paragraph');
  });

  it('makes a changed custom command reversible even when it omits an inverse', () => {
    const bus = new DesignerCommandBus(document);
    const command = {
      id: 'custom-metadata',
      label: 'Custom metadata',
      execute: ({ document: current }: { document: DesignerDocument }) => ({
        changed: true,
        diagnostics: [],
        document: { ...current, metadata: { ...current.metadata, changed: true } }
      })
    };

    expect(bus.execute(command).changed).toBe(true);
    expect(bus.document.metadata.changed).toBe(true);
    expect(bus.undo()?.changed).toBe(true);
    expect(bus.document.metadata.changed).toBeUndefined();
    expect(bus.redo()?.changed).toBe(true);
    expect(bus.document.metadata.changed).toBe(true);
  });

  it('preserves an explicit patch inverse with additional conflict guards', () => {
    const bus = new DesignerCommandBus(document);
    const next = { ...document, metadata: { ...document.metadata, changed: true } };
    const inverse = invertDesignerDocumentPatch(createDesignerDocumentPatch(document, next));
    const guardedInverse = {
      ...inverse,
      fieldChanges: [...inverse.fieldChanges, {
        key: 'runtimeContext' as const,
        beforePresent: true,
        afterPresent: true,
        before: structuredClone(document.runtimeContext),
        after: structuredClone(document.runtimeContext)
      }]
    };
    const command: DesignerCommand = {
      id: 'guarded-inverse',
      label: 'Guarded inverse',
      execute: ({ document: current }) => ({
        changed: true,
        diagnostics: [],
        document: { ...current, metadata: { ...current.metadata, changed: true } },
        inverse: createDesignerDocumentPatchCommand('guarded-inverse:inverse', 'Undo guarded inverse', guardedInverse)
      })
    };

    const result = bus.execute(command);

    expect((result.inverse as DesignerPatchCommand).patch.fieldChanges.map((change) => change.key)).toEqual(['metadata', 'runtimeContext']);
    expect(bus.undo()?.changed).toBe(true);
    expect(bus.document.metadata.changed).toBeUndefined();
  });

  it('replays a stored patch on redo instead of invoking a custom command again', () => {
    let executions = 0;
    const command: DesignerCommand = {
      id: 'single-use-command',
      label: 'Single-use command',
      execute: ({ document: current }) => {
        executions += 1;
        return executions === 1
          ? { changed: true, diagnostics: [], document: { ...current, metadata: { ...current.metadata, replayed: true } } }
          : { changed: false, diagnostics: ['command was re-executed'], document: current };
      }
    };
    const bus = new DesignerCommandBus(document);

    bus.execute(command);
    bus.undo();
    const result = bus.redo();

    expect(result?.changed).toBe(true);
    expect(executions).toBe(1);
    expect(bus.document.metadata.replayed).toBe(true);
  });

  it('replays one patch for a continuous merge instead of recomposing commands', () => {
    let executions = 0;
    const createCommand = (title: string): DesignerCommand => ({
      id: `title-${title}`,
      label: `Set title ${title}`,
      mergeKey: 'child:title',
      execute: ({ document: current }) => {
        executions += 1;
        return { changed: true, diagnostics: [], document: { ...current, metadata: { ...current.metadata, title } } };
      }
    });
    const bus = new DesignerCommandBus(document);

    bus.execute(createCommand('Draft'));
    bus.execute(createCommand('Published'));
    bus.endTransaction();
    bus.undo();
    const result = bus.redo();

    expect(result?.changed).toBe(true);
    expect(executions).toBe(2);
    expect(bus.document.metadata.title).toBe('Published');
  });

  it('replays an explicit transaction as one patch', () => {
    let executions = 0;
    const createCommand = (key: string, value: string): DesignerCommand => ({
      id: `transaction-${key}`,
      label: `Set ${key}`,
      execute: ({ document: current }) => {
        executions += 1;
        return { changed: true, diagnostics: [], document: { ...current, metadata: { ...current.metadata, [key]: value } } };
      }
    });
    const bus = new DesignerCommandBus(document);

    bus.executeTransaction([createCommand('first', 'one'), createCommand('second', 'two')]);
    bus.undo();
    const result = bus.redo();

    expect(result?.changed).toBe(true);
    expect(executions).toBe(2);
    expect(bus.document.metadata).toMatchObject({ first: 'one', second: 'two' });
  });

  it('writes responsive commands to the runtime layout contract', () => {
    const bus = new DesignerCommandBus(document);
    bus.execute(createPatchResponsiveOverrideCommand('child', 'tablet', { layout: { width: 640 }, style: { minHeight: 240 } }));
    expect(new LayoutResolver().resolveSections(bus.document.elements.child, { breakpoint: 'tablet' })).toMatchObject({
      layout: { width: 640 },
      style: { minHeight: 240 }
    });
    expect(bus.document.elements.child.layout.constraints).toBeUndefined();
  });

  it('deep-merges responsive sections and clears a field back to the base layout', () => {
    const base = { ...document, elements: { ...document.elements, child: { ...document.elements.child, layout: { width: 320 }, props: { title: 'Base' } } } };
    const bus = new DesignerCommandBus(base);
    bus.execute(createPatchResponsiveOverrideCommand('child', 'tablet', { layout: { width: 640 }, props: { title: 'Tablet' }, style: { border: { color: 'red' } } }));
    bus.execute(createPatchResponsiveOverrideCommand('child', 'tablet', { layout: { x: 24 }, style: { border: { width: 1 } } }));
    expect(bus.document.elements.child.responsiveOverrides?.tablet).toEqual({ layout: { width: 640, x: 24 }, props: { title: 'Tablet' }, style: { border: { color: 'red', width: 1 } } });
    bus.execute(createPatchResponsiveOverrideCommand('child', 'tablet', { layout: { width: undefined } }));
    expect(bus.document.elements.child.responsiveOverrides?.tablet.layout).toEqual({ x: 24 });
    expect(new LayoutResolver().resolve(bus.document.elements.child, { breakpoint: 'tablet' })).toMatchObject({ width: 320, x: 24 });
  });

  it('merges three continuous edits into one reversible-patch undo', () => {
    const bus = new DesignerCommandBus(document);
    bus.execute(createPatchNodeCommand('child', { props: { title: 'O' } }, 'child:title'));
    bus.execute(createPatchNodeCommand('child', { props: { title: 'Orders' } }, 'child:title'));
    bus.execute(createPatchNodeCommand('child', { props: { title: 'Orders 2026' } }, 'child:title'));
    bus.endTransaction();
    expect(bus.document.elements.child.props.title).toBe('Orders 2026');
    const undone = bus.undo();
    expect(undone?.changed).toBe(true);
    expect(bus.document.elements.child.props.title).toBeUndefined();
    expect(bus.redo()?.changed).toBe(true);
    expect(bus.document.elements.child.props.title).toBe('Orders 2026');
  });

  it('flushes a continuous merge before a following ordinary command without explicit endTransaction', () => {
    const bus = new DesignerCommandBus(document);
    const initialHash = bus.document.documentHash;

    bus.execute(createPatchNodeCommand('child', { props: { title: 'Draft' } }, 'child:title'));
    bus.execute(createPatchNodeCommand('child', { props: { title: 'Published' } }, 'child:title'));
    const mergedContent = contentOf(bus.document);
    const mergedHash = bus.document.documentHash;
    const ordinaryResult = bus.execute(createPatchNodeCommand('child', { type: 'text.heading' }));
    const ordinaryContent = contentOf(bus.document);
    const ordinaryHash = bus.document.documentHash;

    expect(ordinaryResult.changed).toBe(true);
    expect(bus.document.revision).toBe(4);
    expect(ordinaryHash).not.toBe(mergedHash);
    expect(ordinaryContent).not.toEqual(mergedContent);
    expect(bus.document.documentHash).toBe(computeDesignerDocumentHash(bus.document));

    expect(bus.undo()?.changed).toBe(true);
    expect(bus.document.elements.child.type).toBe('text.paragraph');
    expect(bus.document.elements.child.props.title).toBe('Published');
    expect(contentOf(bus.document)).toEqual(mergedContent);
    expect(bus.document.documentHash).not.toBe(mergedHash);
    expect(bus.document.documentHash).toBe(computeDesignerDocumentHash(bus.document));
    expect(bus.document.revision).toBe(5);

    expect(bus.undo()?.changed).toBe(true);
    expect(bus.document.elements.child.props.title).toBeUndefined();
    expect(contentOf(bus.document)).toEqual(contentOf(document));
    expect(bus.document.documentHash).not.toBe(initialHash);
    expect(bus.document.documentHash).toBe(computeDesignerDocumentHash(bus.document));
    expect(bus.document.revision).toBe(6);

    expect(bus.redo()?.changed).toBe(true);
    expect(bus.document.elements.child.props.title).toBe('Published');
    expect(bus.document.elements.child.type).toBe('text.paragraph');
    expect(contentOf(bus.document)).toEqual(mergedContent);
    expect(bus.document.documentHash).not.toBe(mergedHash);
    expect(bus.document.documentHash).toBe(computeDesignerDocumentHash(bus.document));
    expect(bus.document.revision).toBe(7);

    expect(bus.redo()?.changed).toBe(true);
    expect(bus.document.elements.child.type).toBe('text.heading');
    expect(contentOf(bus.document)).toEqual(ordinaryContent);
    expect(bus.document.documentHash).not.toBe(ordinaryHash);
    expect(bus.document.documentHash).toBe(computeDesignerDocumentHash(bus.document));
    expect(bus.document.revision).toBe(8);
  });

  it('does not let a failed command overlap the active merge with the next command', () => {
    const bus = new DesignerCommandBus(document);

    bus.execute(createPatchNodeCommand('child', { props: { title: 'Published' } }, 'child:title'));
    const failed = bus.execute(createDuplicateSubtreeCommand('root', 'child'));

    expect(failed.changed).toBe(false);
    expect(bus.document.elements.child.props.title).toBe('Published');
    expect(bus.document.revision).toBe(2);

    bus.execute(createPatchNodeCommand('child', { type: 'text.heading' }));
    expect(bus.undo()?.changed).toBe(true);
    expect(bus.document.elements.child.type).toBe('text.paragraph');
    expect(bus.document.elements.child.props.title).toBe('Published');
    expect(bus.undo()?.changed).toBe(true);
    expect(bus.document.elements.child.props.title).toBeUndefined();
  });

});

function contentOf(value: DesignerDocument): Omit<DesignerDocument, 'revision' | 'documentHash'> {
  const { revision: _revision, documentHash: _documentHash, ...content } = value;
  void _revision;
  void _documentHash;
  return content;
}

function nestedDocument(depth: number): DesignerDocument {
  const elements: DesignerDocument['elements'] = {};
  const root = { children: ['level-1'], events: [], id: 'root', layout: { x: 10, y: 20 }, parentId: null, props: {}, type: 'layout.page' };
  elements.root = root;
  for (let index = 1; index <= depth; index += 1) {
    const id = `level-${index}`;
    elements[id] = { children: index === depth ? [] : [`level-${index + 1}`], events: [], id, layout: { x: index * 3, y: index * 5 }, parentId: index === 1 ? 'root' : `level-${index - 1}`, props: {}, type: 'layout.container' };
  }
  return { ...document, elements };
}
