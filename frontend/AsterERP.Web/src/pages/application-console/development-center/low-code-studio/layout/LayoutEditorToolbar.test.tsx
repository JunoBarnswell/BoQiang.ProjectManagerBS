// @vitest-environment jsdom

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { DesignerCommandBus } from '../commands/DesignerCommandBus';
import type { DesignerDocument } from '../document/DesignerDocument';

import { LayoutEditorToolbar } from './LayoutEditorToolbar';

describe('LayoutEditorToolbar', () => {
  it('exposes mode switching and mode-specific controls through the real CommandBus', () => {
    const bus = new DesignerCommandBus(createDocument());
    const view = render(<LayoutEditorToolbar commandBus={bus} document={bus.document} selectedNodeIds={['left', 'right']} />);

    fireEvent.click(screen.getByRole('button', { name: 'Grid' }));
    view.rerender(<LayoutEditorToolbar commandBus={bus} document={bus.document} selectedNodeIds={['left', 'right']} />);

    expect(bus.document.elements.root.layout.container?.mode).toBe('grid');
    expect(screen.getAllByRole('button').some((button) => (button as HTMLButtonElement).disabled)).toBe(true);
    expect(screen.getByLabelText('Grid 列数')).toBeTruthy();
    expect(screen.getByRole('button', { name: '水平居中' })).toBeTruthy();
  });
  it('exposes same-size operations in free layout and applies one command', () => {
    const bus = new DesignerCommandBus(createDocument());
    const view = render(<LayoutEditorToolbar commandBus={bus} document={bus.document} selectedNodeIds={['left', 'right']} />);
    const sameWidth = view.container.querySelector<HTMLButtonElement>('[data-layout-operation="same-width"]');
    expect(sameWidth?.getAttribute('data-layout-operation')).toBe('same-width');
    fireEvent.click(sameWidth!);

    expect(bus.document.elements.left.layout.width).toBe(80);
    expect(bus.document.elements.right.layout.width).toBe(80);
  });

  it('moves a flex child exactly one position using after-detach indices', () => {
    const document = createDocument();
    document.elements.root.layout = { layoutMode: 'flex' };
    document.elements.middle = { id: 'middle', parentId: 'root', children: [], events: [], layout: {}, props: {}, type: 'text' };
    document.elements.root.children = ['left', 'middle', 'right'];
    const bus = new DesignerCommandBus(document);
    const view = render(<LayoutEditorToolbar commandBus={bus} document={bus.document} selectedNodeIds={['middle']} />);

    fireEvent.click(screen.getByRole('button', { name: 'Move child down' }));
    expect(bus.document.elements.root.children).toEqual(['left', 'right', 'middle']);
    view.rerender(<LayoutEditorToolbar commandBus={bus} document={bus.document} selectedNodeIds={['middle']} />);
    fireEvent.click(screen.getByRole('button', { name: 'Move child up' }));
    expect(bus.document.elements.root.children).toEqual(['left', 'middle', 'right']);
  });
});

function createDocument(): DesignerDocument {
  return {
    actions: [], apiBindings: [], dataSources: [], documentId: 'toolbar-test', elements: {
      root: { id: 'root', parentId: null, children: ['left', 'right'], events: [], layout: { layoutMode: 'free' }, props: {}, type: 'layout.container' },
      left: { id: 'left', parentId: 'root', children: [], events: [], layout: { x: 10, y: 10, width: 80, height: 20 }, props: {}, type: 'text' },
      right: { id: 'right', parentId: 'root', children: [], events: [], layout: { x: 120, y: 50, width: 120, height: 20 }, props: {}, type: 'text' }
    },
    metadata: {}, modals: [], pageParameters: [], pageType: 'page', pages: [{ id: 'page', name: 'Page', rootElementId: 'root' }], permissions: {}, revision: 1, runtimeContext: {}, styleTokens: {}, variables: [], workflowBindings: []
  };
}
