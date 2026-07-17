import { describe, expect, it } from 'vitest';

import { buildResourceOptions, resourceOptionToExpression } from './expressionCatalog';
import type { BindingDocument } from './expressionTypes';

const document: BindingDocument = {
  apiBindings: [{ id: 'orders', name: 'Orders API' }],
  elements: {
    customerName: { bindings: { data: { field: 'customer.name' } }, id: 'customerName', name: 'Customer name', type: 'input.text' }
  },
  pageParameters: [{ code: 'tenantId', direction: 'input', name: 'Tenant', valueType: 'string' }],
  runtimeContext: { pageCode: 'orders' },
  variables: [{ id: 'count', name: 'Count', source: 'variable', valueType: 'number' }],
  workflowBindings: [{ id: 'approval', name: 'Approval' }]
};

describe('latest expression resource catalog', () => {
  it('exposes runtime, page, form, variable and binding resources with stable IDs', () => {
    const options = buildResourceOptions(document);
    expect(options.map((option) => option.id)).toEqual(expect.arrayContaining([
      'system:currentUser',
      'page:inputs.tenantId',
      'form:customer.name',
      'variables:count',
      'api:orders',
      'workflow:approval'
    ]));
  });

  it('creates a minimal resource-ID editing projection without legacy source-path metadata', () => {
    const option = buildResourceOptions(document).find((item) => item.id === 'variables:count');
    expect(option).toBeDefined();
    expect(resourceOptionToExpression(option!)).toEqual({
      expectedType: 'number',
      fallback: 0,
      resourceId: 'variables:count',
      resourceType: 'variables'
    });
  });

  it('keeps the identity when only a variable display name changes', () => {
    const renamed = buildResourceOptions({ ...document, variables: [{ id: 'count', name: 'Renamed count', source: 'variable', valueType: 'number' }] });
    expect(renamed.find((item) => item.label === 'Renamed count')?.id).toBe('variables:count');
  });
});
