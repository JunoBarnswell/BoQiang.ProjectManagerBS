// @vitest-environment jsdom

import { describe, expect, it } from 'vitest';

import type { RuntimeArtifact } from '../pages/application-console/development-center/low-code-studio/document/RuntimeArtifact';

import { mergeRuntimePageMicroflowFormDefaults } from './RuntimePageMicroflowFormDefaults';

describe('RuntimePageMicroflowFormDefaults', () => {
  it('fills empty bound fields without overwriting user input', () => {
    const document = designerDocument({
      code: inputElement('code', 'code', 'orders.data.customerCode'),
      name: inputElement('name', 'name', 'orders.data.customerName')
    });
    const result = mergeRuntimePageMicroflowFormDefaults(document, { code: 'typed code' }, {
      microflows: { orders: { data: { customerCode: 'C001', customerName: 'Customer' } } }
    });
    expect(result).toEqual({ code: 'typed code', name: 'Customer' });
  });

  it('overwrites bound fields only when the action explicitly requests it', () => {
    const document = designerDocument({
      code: inputElement('code', 'code', 'orders.data.customerCode'),
      name: inputElement('name', 'name', 'orders.data.customerName')
    });
    const result = mergeRuntimePageMicroflowFormDefaults(document, { code: 'typed code', name: 'typed name' }, {
      microflows: { orders: { data: { customerCode: 'C001', customerName: 'Customer' } } }
    }, { overwrite: true });
    expect(result).toEqual({ code: 'C001', name: 'Customer' });
  });
});

function designerDocument(elements: RuntimeArtifact['elements']): Pick<RuntimeArtifact, 'elements'> {
  return { elements };
}

function inputElement(id: string, field: string, path: string): RuntimeArtifact['elements'][string] {
  return {
    children: [],
    bindings: { data: { field, displayName: `microflow.${path}`, resourceId: `microflow::${path}`, valueType: 'string' } },
    events: [],
    id,
    layout: {},
    name: id,
    parentId: null,
    props: {},
    type: 'input.text'
  };
}
