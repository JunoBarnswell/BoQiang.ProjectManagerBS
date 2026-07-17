import { describe, expect, it } from 'vitest';

import type { DesignerDocumentNode } from '../../pages/application-console/development-center/low-code-studio/document/DesignerDocument';

import { mapDesignerNodeToRuntimeElement, type RuntimeRenderBoundaryNode } from './RuntimeRenderBoundaryContract';

describe('RuntimeRenderBoundaryContract', () => {
  it('maps resolved runtime fields instead of leaking Designer node layout or children', () => {
    const source: DesignerDocumentNode = {
      children: ['designer-child'],
      events: [{ id: 'save', name: ' Save ', trigger: 'click', steps: [{ config: { target: 'saved' }, id: 'step-1', type: 'setVariable' }] }],
      id: 'field-1',
      layout: { height: 20, width: 80 },
      name: ' Customer name ',
      parentId: 'designer-parent',
      props: { title: 'Designer title' },
      responsiveOverrides: { tablet: { layout: { height: 40, width: 200 } } },
      style: { color: 'red' },
      type: 'input.text'
    };
    const runtime: RuntimeRenderBoundaryNode = {
      bindings: { data: { resourceId: 'form:customerName' } },
      children: ['runtime-child'],
      disabled: false,
      id: 'field-1',
      layout: { height: 40, width: 200 },
      loading: false,
      permission: { code: 'customer.read', visibleWhen: 'true' },
      props: { title: 'Resolved title', value: '当前值' },
      readOnly: false,
      style: { color: 'blue' },
      type: 'input.text',
      visible: true
    };

    const element = mapDesignerNodeToRuntimeElement(source, runtime);

    expect(element).toEqual({
      bindings: { data: { resourceId: 'form:customerName' } },
      children: ['runtime-child'],
      events: [{ id: 'save', name: 'Save', steps: [{ config: { target: 'saved' }, id: 'step-1', type: 'setVariable' }], trigger: 'click' }],
      id: 'field-1',
      layout: { height: 40, width: 200 },
      name: 'Customer name',
      parentId: 'designer-parent',
      permission: { code: 'customer.read', visibleWhen: 'true' },
      props: { title: 'Resolved title', value: '当前值' },
      style: { color: 'blue' },
      type: 'input.text'
    });
    expect(element).not.toHaveProperty('responsiveOverrides');
    expect(element.layout).not.toBe(runtime.layout);
    expect(element.children).not.toBe(runtime.children);
  });
});
