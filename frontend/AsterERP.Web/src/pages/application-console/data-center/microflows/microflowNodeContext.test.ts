import { describe, expect, it } from 'vitest';

import type { MicroflowDefinition, MicroflowDomainField, MicroflowValueExpression } from '../../../../api/application-data-center/applicationDataCenter.types';

import { normalizeMicroflowDefinitionForSave } from './microflowDefinitionNormalizer';
import {
  applyReturnOutputSchema,
  listMicroflowContextVariables,
  listNodeInputReferenceOptions,
  listNodeOutputSchemaOptions,
  readNodeOutputSchema,
  readReturnOutputSchema
} from './microflowNodeContext';

describe('microflowNodeContext', () => {
  it('lists upstream query target variables with configured output fields', () => {
    const definition = createDefinition();

    const contexts = listMicroflowContextVariables(definition, 'return');

    const queryContext = contexts.find((context) => context.id === 'node:query:orderListRows');
    expect(queryContext?.variableCode).toBe('orderListRows');
    expect(queryContext?.valueType).toBe('array');
    expect(queryContext?.fields.map((field) => field.fieldCode)).toEqual(['orderNo', 'amount']);
    expect(contexts.filter((context) => context.variableCode === 'orderListRows')).toHaveLength(1);
    expect(contexts.some((context) => context.sourceKind === 'variable' && context.variableCode === 'orderListRows')).toBe(false);
  });

  it('saves return output schema to node config and definition outputs', () => {
    const definition = createDefinition();
    const next = applyReturnOutputSchema(definition, 'return', {
      fields: [field('orderNo', '订单号')],
      valueType: 'array',
      variableCode: 'orderListRows',
      variableName: '订单列表'
    });

    const returnNode = next.nodes.find((node) => node.id === 'return');
    expect(returnNode?.config.outputSchema).toBeDefined();
    expect(returnNode?.config.outputSchema).toMatchObject({
      valueType: 'array',
      variableCode: 'orderListRows',
      variableName: '订单列表'
    });
    const savedOutput = next.outputs.find((output) => output.variableCode === 'orderListRows');
    if (!savedOutput) {
      throw new Error('Expected orderListRows output');
    }

    expect(savedOutput.fields?.map((item) => item.fieldCode)).toEqual(['orderNo']);
  });

  it('reads return schema only from outputSchema', () => {
    const definition = createDefinition();
    const returnNode = definition.nodes.find((node) => node.id === 'return');

    const schema = returnNode ? readReturnOutputSchema(definition, returnNode) : null;

    expect(schema?.variableCode).toBe('orderListRows');
    expect(schema?.fields.map((item) => item.fieldCode)).toEqual(['orderNo', 'amount']);
  });

  it('does not infer return schema when outputSchema is missing', () => {
    const definition: MicroflowDefinition = {
      ...createDefinition(),
      nodes: createDefinition().nodes.map((node) => node.id === 'return'
        ? {
            ...node,
            config: {}
          }
        : node)
    };
    const returnNode = definition.nodes.find((node) => node.id === 'return');

    const schema = returnNode ? readReturnOutputSchema(definition, returnNode) : null;

    expect(schema).toBeNull();
  });

  it('normalizes dotted set variable targets before save', () => {
    const definition: MicroflowDefinition = {
      ...createDefinition(),
      outputs: [
        { defaultValue: [], fields: [], valueType: 'array', variableCode: 'orderListRows', variableName: '订单列表' },
        ...createDefinition().outputs
      ],
      variables: [
        { defaultValue: {}, fields: [field('product_name', '产品名称')], valueType: 'object', variableCode: 'acceptance.loopLastItem', variableName: '最后循环项' }
      ],
      nodes: [
        ...createDefinition().nodes,
        {
          config: {
            outputSchema: {
              fields: [field('product_name', '产品名称')],
              valueType: 'object',
              variableCode: 'acceptance.loopLastItem',
              variableName: '最后循环项'
            },
            valueExpression: refExpr('item', '', 'object'),
            variableCode: 'acceptance.loopLastItem'
          },
          id: 'setVariable',
          name: '记录最后循环项',
          type: 'setVariable',
          x: 0,
          y: 0
        }
      ]
    };

    const next = normalizeMicroflowDefinitionForSave(definition);
    const setVariable = next.nodes.find((node) => node.id === 'setVariable');

    expect(next.outputs.map((output) => output.variableCode)).toEqual(['orderListRows']);
    expect(next.variables.map((variable) => variable.variableCode)).toEqual(['acceptance']);
    expect(next.variables[0]?.fields?.map((item) => item.fieldCode)).toEqual(['loopLastItem', 'loopLastItem.product_name']);
    expect(setVariable?.config.outputSchema).toMatchObject({
      variableCode: 'acceptance',
      valueType: 'object'
    });
    expect((setVariable?.config.outputSchema as { fields?: MicroflowDomainField[] }).fields?.map((item) => item.fieldCode))
      .toEqual(['loopLastItem', 'loopLastItem.product_name']);
  });

  it('expands all connected upstream fields as input references', () => {
    const definition = createDefinition([
      field('orderNo', '订单号'),
      field('amount', '金额', 'number'),
      field('customerName', '客户名称'),
      field('status', '状态')
    ]);

    const references = listNodeInputReferenceOptions(definition, 'return');

    expect(references.filter((item) => item.isField).map((item) => item.field?.fieldCode)).toEqual([
      'orderNo',
      'amount',
      'customerName',
      'status'
    ]);
  });

  it('does not expose unrelated node outputs when the node is not connected', () => {
    const definition: MicroflowDefinition = {
      ...createDefinition(),
      edges: [],
      nodes: [
        ...createDefinition().nodes,
        { config: { targetVariable: 'unrelatedRows' }, id: 'unrelated', name: '无关查询', type: 'query', x: 420, y: 0 }
      ]
    };

    const references = listNodeInputReferenceOptions(definition, 'return');

    expect(references.some((item) => item.sourceVariableCode === 'unrelatedRows')).toBe(false);
    expect(references.some((item) => item.sourceKind === 'variable' && item.sourceVariableCode === 'orderListRows')).toBe(true);
  });

  it('does not offer the current node target variable as its own input reference', () => {
    const definition: MicroflowDefinition = {
      ...createDefinition(),
      inputs: [
        {
          defaultValue: '',
          fields: [],
          schemaObjectCode: null,
          valueType: 'string',
          variableCode: 'customerName',
          variableName: '客户名称'
        }
      ]
    };

    const references = listNodeInputReferenceOptions(definition, 'query');

    expect(references.some((item) => item.sourceVariableCode === 'customerName')).toBe(true);
    expect(references.some((item) => item.sourceVariableCode === 'orderListRows')).toBe(false);
  });

  it('lists object and array contexts as return output sources', () => {
    const definition = createDefinition();
    const returnNode = definition.nodes.find((node) => node.id === 'return') ?? null;

    const sources = listNodeOutputSchemaOptions(definition, returnNode);

    expect(sources.map((item) => item.variableCode)).toContain('orderListRows');
  });

  it('uses node output schema fields before global variable fields', () => {
    const customFields = [
      field('customerCode', '客户编码'),
      field('customerName', '客户名称'),
      field('paidAmount', '已付金额', 'number')
    ];
    const definition = {
      ...createDefinition([field('legacyNo', '旧字段')]),
      nodes: createDefinition().nodes.map((node) => node.id === 'query'
        ? {
            ...node,
            config: {
              ...node.config,
              outputSchema: {
                fields: customFields,
                valueType: 'array',
                variableCode: 'orderListRows',
                variableName: '订单列表'
              }
            }
          }
        : node)
    };

    const queryNode = definition.nodes.find((node) => node.id === 'query');
    const schema = queryNode ? readNodeOutputSchema(definition, queryNode) : null;
    const references = listNodeInputReferenceOptions(definition, 'return');

    expect(schema?.fields.map((item) => item.fieldCode)).toEqual(['customerCode', 'customerName', 'paidAmount']);
    expect(references.filter((item) => item.sourceNodeId === 'query' && item.isField).map((item) => item.field?.fieldCode)).toEqual([
      'customerCode',
      'customerName',
      'paidAmount'
    ]);
  });

  it('exposes setVariable variableCode as downstream output context', () => {
    const definition: MicroflowDefinition = {
      ...createDefinition(),
      edges: [
        { id: 'start_set', sourceNodeId: 'start', targetNodeId: 'set' },
        { id: 'set_return', sourceNodeId: 'set', targetNodeId: 'return' }
      ],
      nodes: [
        { config: {}, id: 'start', name: 'Start', type: 'start', x: 0, y: 0 },
        {
          config: {
            outputSchema: {
              fields: [field('approved', '是否通过', 'boolean')],
              valueType: 'object',
              variableCode: 'approvalResult',
              variableName: '审批结果'
            },
            variableCode: 'approvalResult'
          },
          id: 'set',
          name: '设置审批结果',
          type: 'setVariable',
          x: 120,
          y: 0
        },
        createDefinition().nodes.find((node) => node.id === 'return')!
      ],
      outputs: [],
      variables: []
    };

    const references = listNodeInputReferenceOptions(definition, 'return');

    expect(references.map((item) => item.label)).toContain('设置审批结果.approvalResult.approved');
  });

  it('exposes loop item variable fields from the collection source', () => {
    const definition: MicroflowDefinition = {
      ...createDefinition(),
      edges: [
        { id: 'start_query', sourceNodeId: 'start', targetNodeId: 'query' },
        { id: 'query_loop', sourceNodeId: 'query', targetNodeId: 'loop' },
        { id: 'loop_change', sourceNodeId: 'loop', targetNodeId: 'change' }
      ],
      nodes: [
        { config: {}, id: 'start', name: 'Start', type: 'start', x: 0, y: 0 },
        {
          config: {
            outputSchema: {
              fields: [field('productId', '产品'), field('qty', '数量', 'number')],
              valueType: 'array',
              variableCode: 'detailLines',
              variableName: '明细行'
            },
            targetVariable: 'detailLines'
          },
          id: 'query',
          name: '查询明细',
          type: 'query',
          x: 120,
          y: 0
        },
        {
          config: {
            collectionExpression: refExpr('variables', 'detailLines', 'array'),
            itemVariable: 'line'
          },
          id: 'loop',
          name: '循环明细',
          type: 'loop',
          x: 260,
          y: 0
        },
        { config: { targetVariable: 'updatedLine' }, id: 'change', name: '更新明细', type: 'change', x: 420, y: 0 }
      ],
      outputs: [],
      variables: []
    };

    const references = listNodeInputReferenceOptions(definition, 'change');

    expect(references.filter((item) => item.sourceVariableCode === 'line' && item.isField).map((item) => item.field?.fieldCode)).toEqual(['productId', 'qty']);
  });

  it('exposes global variable node declarations as visual reference options', () => {
    const definition: MicroflowDefinition = {
      ...createDefinition(),
      nodes: [
        ...createDefinition().nodes,
        {
          config: {
            variables: [
              {
                defaultValue: 3,
                fields: [field('limit', '限制', 'number')],
                schemaObjectCode: null,
                sourceNodeId: 'globals',
                valueType: 'object',
                variableCode: 'acceptance',
                variableName: '验收变量'
              }
            ]
          },
          id: 'globals',
          name: '全局变量',
          type: 'globalVariables',
          x: 80,
          y: 220
        }
      ],
      variables: []
    };

    const references = listNodeInputReferenceOptions(definition, 'return');

    expect(references.map((item) => item.label)).toContain('variables.acceptance');
    expect(references.map((item) => item.label)).toContain('variables.acceptance.limit');
  });

  it('exposes composite child row fields as currentRow visual options', () => {
    const definition: MicroflowDefinition = {
      ...createDefinition(),
      edges: [
        { id: 'start_composite', sourceNodeId: 'start', targetNodeId: 'composite' },
        { id: 'composite_return', sourceNodeId: 'composite', targetNodeId: 'return' }
      ],
      nodes: [
        { config: {}, id: 'start', name: 'Start', type: 'start', x: 0, y: 0 },
        {
          config: {
            children: [
              {
                fieldMappings: [],
                rowsExpression: literalExpr([{ product_name: '测试产品', qty: 2 }], 'array')
              }
            ],
            targetVariable: 'compositeResult'
          },
          id: 'composite',
          name: '复合保存',
          type: 'compositeCreate',
          x: 120,
          y: 0
        },
        createDefinition().nodes.find((node) => node.id === 'return')!
      ]
    };

    const references = listNodeInputReferenceOptions(definition, 'composite');

    expect(references.map((item) => formatExpressionRef(item.expression))).toContain('currentRow.product_name');
    expect(references.map((item) => formatExpressionRef(item.expression))).toContain('currentRow.qty');
  });
});

function formatExpressionRef(expression: MicroflowValueExpression): string {
  const reference = expression.ref;
  if (!reference) {
    return expression.kind;
  }

  return [reference.outputKey || reference.variableId, ...reference.fieldPath].filter(Boolean).join('.');
}

function literalExpr(value: unknown, dataType = 'string'): MicroflowValueExpression {
  return {
    dataType,
    kind: 'literal',
    value
  };
}

function refExpr(source: string, path: string, dataType = 'string'): MicroflowValueExpression {
  const parts = path.split('.').filter(Boolean);
  const sourceType = source === 'variables' ? 'global' : 'loopItem';
  const variableId = source === 'variables' ? parts[0] ?? '' : source;
  return {
    dataType,
    kind: 'ref',
    ref: {
      dataType,
      fieldPath: source === 'variables' ? parts.slice(1) : parts,
      label: path || source,
      outputKey: source === 'variables' ? variableId : source,
      sourceType,
      variableId
    }
  };
}

function createDefinition(outputFields: MicroflowDomainField[] = [field('orderNo', '订单号'), field('amount', '金额', 'number')]): MicroflowDefinition {
  return {
    apiEndpoints: [],
    associations: [],
    dataMappings: [],
    domainObjects: [],
    edges: [
      { id: 'start_query', sourceNodeId: 'start', targetNodeId: 'query' },
      { id: 'query_return', sourceNodeId: 'query', targetNodeId: 'return' }
    ],
    inputs: [],
    nodes: [
      { config: {}, id: 'start', name: 'Start', type: 'start', x: 0, y: 0 },
      { config: { targetVariable: 'orderListRows' }, id: 'query', name: '从列表视图查询', type: 'query', x: 120, y: 0 },
      {
        config: {
          outputSchema: {
            fields: outputFields.map((item) => ({
              ...item,
              expression: refExpr('currentRow', item.fieldCode, item.dataType)
            })),
            arrayExpression: refExpr('variables', 'orderListRows', 'array'),
            valueType: 'array',
            variableCode: 'orderListRows',
            variableName: '订单列表'
          }
        },
        id: 'return',
        name: '返回列表',
        type: 'return',
        x: 260,
        y: 0
      }
    ],
    outputs: [
      {
        defaultValue: [],
        fields: outputFields,
        schemaObjectCode: null,
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
        schemaObjectCode: null,
        valueType: 'array',
        variableCode: 'orderListRows',
        variableName: '订单列表'
      }
    ]
  };
}

function field(fieldCode: string, fieldName: string, dataType = 'string'): MicroflowDomainField {
  return {
    dataType,
    fieldCode,
    fieldName,
    visible: true,
    writable: false
  };
}
