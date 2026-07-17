import type { MicroflowDefinition, MicroflowNodeType, MicroflowValueExpression } from '../../../../api/application-data-center/applicationDataCenter.types';

export const microflowNodeCatalog: Array<{ description: string; title: string; type: MicroflowNodeType }> = [
  { description: '微流入口', title: 'Start', type: 'start' },
  { description: '微流结束', title: 'End', type: 'end' },
  { description: 'if 条件判断', title: 'Decision', type: 'decision' },
  { description: 'for each 循环', title: 'Loop', type: 'loop' },
  { description: '查询模型集合', title: 'Query', type: 'query' },
  { description: '查询模型集合', title: 'Retrieve', type: 'retrieve' },
  { description: '按主键读取模型详情', title: 'Detail', type: 'detail' },
  { description: '按主键读取一对多主从详情', title: 'Composite Detail', type: 'compositeDetail' },
  { description: '创建数据', title: 'Create', type: 'create' },
  { description: '一对多保存主从数据', title: 'Composite Create', type: 'compositeCreate' },
  { description: '更新数据', title: 'Change', type: 'change' },
  { description: '一对多更新主从数据', title: 'Composite Update', type: 'compositeUpdate' },
  { description: '删除数据', title: 'Delete', type: 'delete' },
  { description: '联动删除主从数据', title: 'Composite Delete', type: 'compositeDelete' },
  { description: '调用接口', title: 'Call API', type: 'callApi' },
  { description: '设置变量', title: 'Set Variable', type: 'setVariable' },
  { description: '画布级变量定义，不参与连线', title: 'Global Variables', type: 'globalVariables' },
  { description: '返回结果', title: 'Return', type: 'return' }
];

export function createDefaultExpression(source = 'constant', value = ''): MicroflowValueExpression {
  if (source === 'constant') {
    return createLiteralExpression(value, 'string');
  }

  return createRefExpression(source, '', 'string');
}

export function createLiteralExpression(value: unknown, dataType = 'string'): MicroflowValueExpression {
  return {
    dataType,
    kind: 'literal',
    value
  };
}

export function createArrayExpression(source = 'variables', path = 'items'): MicroflowValueExpression {
  return createRefExpression(source, path, 'array');
}

export function createCountExpression(source = 'variables', path = 'items'): MicroflowValueExpression {
  return {
    args: [createArrayExpression(source, path)],
    dataType: 'number',
    functionId: 'count',
    kind: 'function'
  };
}

export function createRefExpression(source: string, path: string, dataType = 'string'): MicroflowValueExpression {
  const parts = path.split('.').filter(Boolean);
  const sourceType = source === 'variables' ? 'global' : source === 'currentRow' || source === 'row' || source === 'item' ? 'loopItem' : source;
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

export function createDefaultMicroflowDefinition(code: string): MicroflowDefinition {
  const startId = `${code}_start`;
  const queryId = `${code}_query`;
  const returnId = `${code}_return`;
  return {
    apiEndpoints: [
      {
        endpointCode: 'query',
        endpointName: '查询接口',
        httpMethod: 'POST',
        permissionCode: '',
        requiresAuthentication: true,
        routePath: `/api/app/${code}/query`,
        startNodeId: startId
      }
    ],
    associations: [],
    dataMappings: [],
    domainObjects: [
      {
        fields: [
          { dataType: 'string', fieldCode: 'id', fieldName: 'ID', readOnly: true, required: true, visible: false, writable: false },
          { dataType: 'string', fieldCode: 'name', fieldName: '名称', required: false, visible: true, writable: true }
        ],
        idGeneration: 'guid',
        keyField: 'id',
        modelCode: '',
        objectCode: `${code}_object`,
        objectName: '业务对象'
      }
    ],
    edges: [
      { id: `${startId}_${queryId}`, sourceNodeId: startId, targetNodeId: queryId },
      { id: `${queryId}_${returnId}`, sourceNodeId: queryId, targetNodeId: returnId }
    ],
    inputs: [],
    nodes: [
      { config: {}, id: startId, name: 'Start', type: 'start', x: 80, y: 160 },
      { config: { modelCode: '', targetVariable: 'items' }, id: queryId, name: 'Retrieve', type: 'retrieve', x: 310, y: 160 },
      { config: { outputSchema: createDefaultReturnOutputSchema() }, id: returnId, name: 'Return', type: 'return', x: 560, y: 160 }
    ],
    outputs: [],
    permissions: {},
    schemaVersion: 1,
    testCases: [],
    variables: [
      { defaultValue: [], fields: [], schemaObjectCode: null, valueType: 'array', variableCode: 'items', variableName: '查询结果' },
      { defaultValue: {}, fields: [], schemaObjectCode: null, valueType: 'object', variableCode: 'currentRow', variableName: '当前行' },
      { defaultValue: {}, fields: [], schemaObjectCode: null, valueType: 'object', variableCode: 'form', variableName: '表单数据' }
    ]
  };
}

export function createDefaultMicroflowNodeConfig(type: string): Record<string, unknown> {
  if (type === 'retrieve' || type === 'query') {
    return { modelCode: '', pageIndex: 1, pageSize: 20, targetVariable: 'items' };
  }

  if (type === 'detail') {
    return {
      idExpression: createRefExpression('currentRow', '__runtimeKey', 'string'),
      modelCode: '',
      targetVariable: 'detail'
    };
  }

  if (type === 'delete') {
    return {
      idExpression: createRefExpression('currentRow', '__runtimeKey', 'string'),
      modelCode: '',
      targetVariable: 'deleteResult'
    };
  }

  if (type === 'create') {
    return {
      fieldMappings: [],
      modelCode: '',
      targetVariable: 'createdRow'
    };
  }

  if (type === 'change') {
    return {
      fieldMappings: [],
      idExpression: createRefExpression('currentRow', '__runtimeKey', 'string'),
      modelCode: '',
      targetVariable: 'updatedRow'
    };
  }

  if (type === 'compositeDetail') {
    return {
      children: [],
      idExpression: createRefExpression('currentRow', '__runtimeKey', 'string'),
      rootModelCode: '',
      targetVariable: 'compositeDetail'
    };
  }

  if (type === 'compositeCreate') {
    return {
      children: [],
      fieldMappings: [],
      rootModelCode: '',
      targetVariable: 'compositeResult'
    };
  }

  if (type === 'compositeUpdate') {
    return {
      children: [],
      fieldMappings: [],
      idExpression: createRefExpression('currentRow', '__runtimeKey', 'string'),
      rootModelCode: '',
      targetVariable: 'compositeUpdateResult'
    };
  }

  if (type === 'compositeDelete') {
    return { children: [], rootModelCode: '', targetVariable: 'deleteResult' };
  }

  if (type === 'loop') {
    return {
      bodyNodeId: '',
      collectionExpression: createArrayExpression('variables', 'items'),
      itemVariable: 'item'
    };
  }

  if (type === 'decision') {
    return {
      conditionMode: 'all',
      conditionNot: false,
      conditionRules: [
        {
          leftExpression: createLiteralExpression(true, 'boolean'),
          operator: 'equals',
          rightExpression: createLiteralExpression(true, 'boolean')
        }
      ]
    };
  }

  if (type === 'callApi') {
    return {
      bodyMappings: [],
      httpMethod: 'GET',
      queryMappings: [],
      routePath: '',
      targetVariable: 'apiResult'
    };
  }

  if (type === 'setVariable') {
    return {
      valueExpression: createDefaultExpression('constant', ''),
      variableCode: 'nextValue'
    };
  }

  if (type === 'globalVariables') {
    return { variables: [] };
  }

  if (type === 'return') {
    return { outputSchema: createDefaultReturnOutputSchema() };
  }

  return {};
}

function createDefaultReturnOutputSchema(): Record<string, unknown> {
  return {
    fields: [
      {
        dataType: 'number',
        fieldCode: 'total',
        fieldName: '总数',
        readOnly: true,
        required: false,
        expression: createCountExpression('variables', 'items'),
        visible: true,
        writable: false
      }
    ],
    arrayExpression: null,
    valueType: 'object',
    variableCode: 'result',
    variableName: '返回结果'
  };
}

export function normalizeMicroflowCode(value: string) {
  const normalized = value.trim().replace(/[^A-Za-z0-9_]+/g, '_').replace(/^_+|_+$/g, '').toLowerCase();
  return normalized || `microflow_${Date.now().toString(36)}`;
}
