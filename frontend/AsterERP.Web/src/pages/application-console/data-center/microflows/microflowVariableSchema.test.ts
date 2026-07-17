import { describe, expect, it } from 'vitest';

import type { MicroflowDefinition, MicroflowVariable } from '../../../../api/application-data-center/applicationDataCenter.types';

import {
  createInitialVariableValues,
  listPreviewInputVariables,
  serializeVariableValues,
  validateVariableValues
} from './microflowVariableSchema';

describe('microflowVariableSchema', () => {
  it('serializes object and array variables from visual form state', () => {
    const variables = createVariables();
    const values = createInitialVariableValues(variables);
    values.form = { approvalLevel: 'NORMAL', orderNo: 'SO-001' };
    values.detailLines = [
      { price: '12.50', productId: 'P-1', qty: '2' },
      { price: '3', productId: 'P-2', qty: '5' }
    ];

    const serialized = serializeVariableValues(variables, values);

    expect(serialized.form).toEqual({ approvalLevel: 'NORMAL', orderNo: 'SO-001' });
    expect(serialized.detailLines).toEqual([
      { price: 12.5, productId: 'P-1', qty: 2 },
      { price: 3, productId: 'P-2', qty: 5 }
    ]);
  });

  it('validates required fields and numeric input before preview run', () => {
    const variables = createVariables();
    const values = createInitialVariableValues(variables);
    values.detailLines = [
      { price: 'abc', productId: '', qty: '2' }
    ];

    const validation = validateVariableValues(variables, values);

    expect(validation.valid).toBe(false);
    expect(validation.errors['detailLines.0.productId']).toBe('必填');
    expect(validation.errors['detailLines.0.price']).toBe('必须是数字');
  });

  it('keeps legacy object and array variables without fields structured', () => {
    const variables: MicroflowVariable[] = [
      { defaultValue: {}, valueType: 'object', variableCode: 'form', variableName: '表单' },
      { defaultValue: [], valueType: 'array', variableCode: 'detailLines', variableName: '明细' }
    ];
    const values = createInitialVariableValues(variables);

    expect(serializeVariableValues(variables, values)).toEqual({
      detailLines: [],
      form: {}
    });
  });

  it('uses configured output fields when an input preview variable shares the output code', () => {
    const definition: MicroflowDefinition = {
      apiEndpoints: [],
      associations: [],
      dataMappings: [],
      domainObjects: [],
      edges: [],
      inputs: [
        {
          defaultValue: [],
          fields: [],
          valueType: 'array',
          variableCode: 'orderListRows',
          variableName: '订单列表'
        }
      ],
      nodes: [],
      outputs: [
        {
          defaultValue: [],
          fields: [
            { dataType: 'string', fieldCode: 'orderNo', fieldName: '订单号', visible: true, writable: true },
            { dataType: 'number', fieldCode: 'amount', fieldName: '金额', visible: true, writable: true }
          ],
          valueType: 'array',
          variableCode: 'orderListRows',
          variableName: '订单列表'
        }
      ],
      permissions: {},
      schemaVersion: 1,
      testCases: [],
      variables: [
        {
          defaultValue: [],
          fields: [],
          valueType: 'array',
          variableCode: 'orderListRows',
          variableName: '订单列表'
        }
      ]
    };

    const variables = listPreviewInputVariables(definition);

    expect(variables).toHaveLength(1);
    expect(variables[0].fields?.map((field) => field.fieldCode)).toEqual(['orderNo', 'amount']);
  });

  it('does not require internal process variables in the preview input form', () => {
    const definition: MicroflowDefinition = {
      apiEndpoints: [],
      associations: [],
      dataMappings: [],
      domainObjects: [],
      edges: [],
      inputs: [
        {
          defaultValue: false,
          fields: [],
          valueType: 'boolean',
          variableCode: 'forceEnd',
          variableName: '进入 End 分支'
        }
      ],
      nodes: [],
      outputs: [],
      permissions: {},
      schemaVersion: 1,
      testCases: [],
      variables: [
        {
          defaultValue: {},
          fields: [
            { dataType: 'string', fieldCode: 'orderNo', fieldName: '订单号', required: true, visible: true, writable: true }
          ],
          valueType: 'object',
          variableCode: 'createdRow',
          variableName: '新建结果'
        }
      ]
    };

    const variables = listPreviewInputVariables(definition);
    const values = createInitialVariableValues(variables);
    const validation = validateVariableValues(variables, values);

    expect(variables.map((variable) => variable.variableCode)).toEqual(['forceEnd']);
    expect(validation.valid).toBe(true);
    expect(serializeVariableValues(variables, values)).toEqual({ forceEnd: false });
  });
});

function createVariables(): MicroflowVariable[] {
  return [
    {
      defaultValue: {},
      fields: [
        { dataType: 'string', fieldCode: 'orderNo', fieldName: '订单号', required: true, visible: true, writable: true },
        { dataType: 'string', fieldCode: 'approvalLevel', fieldName: '审批级别', visible: true, writable: true }
      ],
      valueType: 'object',
      variableCode: 'form',
      variableName: '表头'
    },
    {
      defaultValue: [],
      fields: [
        { dataType: 'string', fieldCode: 'productId', fieldName: '商品', required: true, visible: true, writable: true },
        { dataType: 'number', fieldCode: 'qty', fieldName: '数量', required: true, visible: true, writable: true },
        { dataType: 'decimal', fieldCode: 'price', fieldName: '单价', visible: true, writable: true }
      ],
      valueType: 'array',
      variableCode: 'detailLines',
      variableName: '明细'
    }
  ];
}
