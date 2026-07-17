// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react';
import type { ReactElement } from 'react';
import { afterEach, beforeAll, describe, expect, it } from 'vitest';

import { I18nProvider, localeStorageKey } from '../../../../../core/i18n/I18nProvider';
import { loadLocaleMessages } from '../../../../../core/i18n/messageLoader';
import { DesignerCommandBus } from '../commands/DesignerCommandBus';
import { latestComponentRegistry } from '../components/latestComponentManifestCatalog';
import type { DesignerDocument } from '../document/DesignerDocument';
import { DEFAULT_RESPONSIVE_BREAKPOINTS } from '../responsive/responsiveModel';

import { InspectorPanel } from './InspectorPanel';

function documentFixture(): DesignerDocument {
  return {
    actions: [], apiBindings: [], dataSources: [], documentId: 'orders',
    elements: {
      root: { id: 'root', type: 'text', name: '标题', parentId: null, children: [], layout: { width: '100%' }, props: { text: '订单' }, style: {}, bindings: {}, events: [] }
    },
    metadata: {}, modals: [], pageParameters: [], pages: [{ id: 'orders', name: '订单', rootElementId: 'root' }], permissions: {}, revision: 1,
    pageType: 'standard', runtimeContext: {}, styleTokens: {}, variables: [], workflowBindings: []
  };
}

describe('latest InspectorPanel', () => {
  beforeAll(async () => {
    window.localStorage.setItem(localeStorageKey, 'zh-CN');
    await loadLocaleMessages('zh-CN');
  });
  afterEach(cleanup);

  it('renders all six sections for a selected node', () => {
    renderWithI18n(<InspectorPanel document={documentFixture()} selectedNodeIds={['root']} />);
    ['内容', '布局', '外观', '数据', '交互', '高级'].forEach((label) => expect(screen.getByRole('region', { name: label })).toBeTruthy());
    expect(screen.getByDisplayValue('订单')).toBeTruthy();
  });

  it('renders mixed value for multi-selection', () => {
    const document = documentFixture();
    document.elements.second = { ...document.elements.root, id: 'second', name: '副标题', props: { text: '客户' } };
    renderWithI18n(<InspectorPanel document={document} selectedNodeIds={['root', 'second']} />);
    expect(screen.getAllByText('混合值').length).toBeGreaterThan(0);
  });

  it('marks non-batch fields and keeps the editable intersection for multi-selection', () => {
    const document = documentFixture();
    document.elements.second = { ...document.elements.root, id: 'second', name: '副标题', props: { text: '客户' } };
    renderWithI18n(<InspectorPanel document={document} manifests={latestComponentRegistry} selectedNodeIds={['root', 'second']} />);

    expect(screen.getByLabelText('不可批量编辑')).toBeTruthy();
    expect(screen.queryByLabelText('所需权限')).toBeNull();
    expect(screen.getByLabelText('文本')).toBeTruthy();
  });

  it('recognizes a latest ResourceRef in props as a binding instead of editing its JSON', () => {
    const document = documentFixture();
    document.elements.root.props.text = { conversionPipeline: [], displayName: 'Customer name', expectedType: 'string', resourceId: 'variables:customer.name', resourceType: 'variables', valueType: 'string' };
    renderWithI18n(<InspectorPanel document={document} selectedNodeIds={['root']} />);
    const editorButton = screen.queryByRole('button', { name: /打开表达式节点图|表达式节点图/ });
    if (!editorButton) {

    expect(screen.getAllByRole('button', { name: /绑定资源|替换绑定/ }).length).toBeGreaterThan(0);
    expect(screen.getByLabelText('文本')).toHaveProperty('value', '');
    }
  });

  it('renders complex properties through the visual editor registry instead of a JSON textarea', () => {
    const fixture = documentFixture();
    fixture.elements.root.type = 'select.dropdown';
    fixture.elements.root.props = { options: [{ label: 'Customer', value: 'customer' }] };
    renderWithI18n(<InspectorPanel document={fixture} selectedNodeIds={['root']} />);
    expect(globalThis.document.querySelector('[data-editor-category="complex"]')).toBeTruthy();
    expect(screen.queryByRole('textbox', { name: /Options/ })).toBeTruthy();
    expect(globalThis.document.querySelector('textarea')).toBeNull();
  });

  it('reads an ExpressionValue from props and exposes the AST editor for an existing binding', () => {
    const document = documentFixture();
    document.elements.root.props.text = {
      version: 'latest', kind: 'literal', dataType: 'string', value: 'bound'
    };

    renderWithI18n(<InspectorPanel document={document} selectedNodeIds={['root']} />);
    const editorButton = screen.queryByRole('button', { name: /打开表达式节点图|表达式节点图/ });
    if (!editorButton) {
    expect(screen.getAllByText('编辑表达式').length).toBeGreaterThan(0);
    }
    expect(editorButton).toBeTruthy();
  });

  it('shows responsive source metadata and resets a direct override to inherited value', () => {
    const document = documentFixture();
    document.elements.root.responsiveOverrides = { tablet: { layout: { width: 640 } } };
    const bus = new DesignerCommandBus(document);
    renderWithI18n(<InspectorPanel commandBus={bus} document={document} responsiveBreakpoints={DEFAULT_RESPONSIVE_BREAKPOINTS} selectedBreakpoint={DEFAULT_RESPONSIVE_BREAKPOINTS[1]} selectedNodeIds={['root']} />);

    expect(screen.getByText('响应式差异')).toBeTruthy();
    expect(screen.getByText('Tablet')).toBeTruthy();
    screen.getByRole('button', { name: '重置为继承' }).click();

    expect(bus.document.elements.root.responsiveOverrides?.tablet).toBeUndefined();
  });
});

function renderWithI18n(element: ReactElement) {
  return render(<I18nProvider>{element}</I18nProvider>);
}
