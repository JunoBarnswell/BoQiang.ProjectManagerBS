import { describe, expect, it } from 'vitest';

import { buildQueryPlanRequest, createInitialQueryModel, createNodeId, normalizeQueryModel, selectNextJoinTarget, validateQueryModel } from './queryModelModel';

const tableResourceId = 'data:table:orders';
const orderResourceId = `${tableResourceId}:column:order-no`;
const statusResourceId = `${tableResourceId}:column:status`;

describe('query model', () => {
  it('creates stable Resource ID node and DTO references without raw SQL', () => {
    const model = createInitialQueryModel('ds-1');
    model.nodes = [{ id: createNodeId('ds-1', tableResourceId), alias: 'o', columns: [{ resourceId: orderResourceId, columnName: 'order_no', dataType: 'TEXT', nullable: false, order: 1, primaryKey: false }], kind: 'table', name: 'orders', resourceId: tableResourceId, x: 80, y: 80 }];
    model.selections = [{ aggregate: 'none', alias: 'orderNo', fieldResourceId: orderResourceId, id: 'selection:1', nodeId: model.nodes[0].id }];
    const request = buildQueryPlanRequest(model);
    expect(request.nodes[0]).toMatchObject({ resourceId: tableResourceId });
    expect(request.columns[0]).toEqual({ alias: 'orderNo', fieldResourceId: orderResourceId, nodeId: model.nodes[0].id });
    expect(request).not.toHaveProperty('objectName');
    expect(request).not.toHaveProperty('rawSql');
  });

  it('reports missing source and output fields', () => {
    const diagnostics = validateQueryModel(createInitialQueryModel());
    expect(diagnostics.filter((item) => item.level === 'error').map((item) => item.message)).toEqual(['Select a data source.', 'Every query node must select a source table or view.', 'Select at least one output field.']);
  });

  it('transmits structured joins, grouping, having, and aggregates through Resource IDs', () => {
    const model = createInitialQueryModel('ds-1');
    const customerTable = 'data:table:customers';
    const customerId = `${customerTable}:column:id`;
    model.nodes = [
      { id: 'orders', alias: 'o', columns: [{ resourceId: orderResourceId, columnName: 'customer_id', dataType: 'TEXT', nullable: false, order: 1, primaryKey: false }, { resourceId: statusResourceId, columnName: 'status', dataType: 'TEXT', nullable: false, order: 2, primaryKey: false }], kind: 'table', name: 'orders', resourceId: tableResourceId, x: 0, y: 0 },
      { id: 'customers', alias: 'c', columns: [{ resourceId: customerId, columnName: 'id', dataType: 'TEXT', nullable: false, order: 1, primaryKey: true }], kind: 'table', name: 'customers', resourceId: customerTable, x: 1, y: 1 }
    ];
    model.joins = [{ id: 'join-1', leftFieldResourceId: orderResourceId, leftNodeId: 'orders', rightFieldResourceId: customerId, rightNodeId: 'customers', type: 'left' }];
    model.selections = [{ aggregate: 'count', alias: 'customerCount', fieldResourceId: customerId, id: 'selection:1', nodeId: 'customers' }];
    model.groupBy = [{ fieldResourceId: statusResourceId, nodeId: 'orders' }];
    const parameterResourceId = 'query-parameter:minimum';
    model.parameters = [{ resourceId: parameterResourceId, name: 'minimum', type: 'number', value: 1 }];
    model.having = [{ fieldResourceId: customerId, id: 'having:1', nodeId: 'customers', operator: 'gt', parameterResourceId }];

    const request = buildQueryPlanRequest(model);

    expect(request.nodes).toHaveLength(2);
    expect(request.joins[0]).toMatchObject({ leftNodeId: 'orders', rightNodeId: 'customers', type: 'left', leftFieldResourceId: orderResourceId, rightFieldResourceId: customerId });
    expect(request.columns[0]).toMatchObject({ aggregate: 'count', nodeId: 'customers', fieldResourceId: customerId });
    expect(request.groupBy).toEqual([{ fieldResourceId: statusResourceId, nodeId: 'orders' }]);
    expect(request.having[0]).toMatchObject({ fieldResourceId: customerId, nodeId: 'customers', parameterResourceId });
  });

  it('rejects a selection whose node and field references are not a pair', () => {
    const model = createInitialQueryModel('ds-1');
    const nodeId = createNodeId('ds-1', tableResourceId);
    model.nodes = [{ id: nodeId, alias: 'o', columns: [{ resourceId: orderResourceId, columnName: 'order_no', dataType: 'TEXT', nullable: false, order: 1, primaryKey: false }], kind: 'table', name: 'orders', resourceId: tableResourceId, x: 0, y: 0 }];
    model.selections = [{ aggregate: 'none', alias: '', fieldResourceId: orderResourceId, id: 'selection:broken', nodeId: '' }];

    expect(validateQueryModel(model).some((item) => item.message.includes('unknown node'))).toBe(true);
    expect(buildQueryPlanRequest(model).columns[0]).toMatchObject({ fieldResourceId: orderResourceId, nodeId: '' });
  });

  it('does not infer a missing node ID during reload, even when the field has one candidate', () => {
    const node = (id: string) => ({ id, alias: id, columns: [{ resourceId: orderResourceId, columnName: 'order_no', dataType: 'TEXT', nullable: false, order: 1, primaryKey: false }], kind: 'table' as const, name: id, resourceId: `${id}:table`, x: 0, y: 0 });
    const model = normalizeQueryModel({ dataSourceId: 'ds-1', nodes: [node('a')], selections: [{ id: 'selection:1', fieldResourceId: orderResourceId, alias: '', aggregate: 'none' }] });

    expect(model.selections[0].nodeId).toBe('');
    expect(validateQueryModel(model).some((item) => item.message.includes('unknown node'))).toBe(true);
  });

  it('creates distinct stable node IDs for repeated resource placements', () => {
    expect(createNodeId('ds-1', tableResourceId)).not.toBe(createNodeId('ds-1', tableResourceId, 2));
    expect(createNodeId('ds-1', tableResourceId, 2)).toContain(':instance:2');
  });

  it('adds each unused table before offering a self-join placement', () => {
    const tables = [{ resourceId: 'orders' }, { resourceId: 'customers' }, { resourceId: 'items' }];
    const root = { resourceId: 'orders' };

    expect(selectNextJoinTarget(tables, [root])).toEqual({ resourceId: 'customers' });
    expect(selectNextJoinTarget(tables, [root, { resourceId: 'customers' }])).toEqual({ resourceId: 'items' });
    expect(selectNextJoinTarget(tables, [root, { resourceId: 'customers' }, { resourceId: 'items' }])).toEqual({ resourceId: 'orders' });
  });

  it('rejects FULL JOIN in the frontend capability diagnostic for MySQL and SQLite', () => {
    const model = createInitialQueryModel('ds-1');
    model.nodes = [
      { id: 'orders', alias: 'o', columns: [{ resourceId: orderResourceId, columnName: 'order_no', dataType: 'TEXT', nullable: false, order: 1, primaryKey: false }], kind: 'table', name: 'orders', resourceId: tableResourceId, x: 0, y: 0 },
      { id: 'customers', alias: 'c', columns: [{ resourceId: statusResourceId, columnName: 'status', dataType: 'TEXT', nullable: false, order: 1, primaryKey: false }], kind: 'table', name: 'customers', resourceId: 'data:table:customers', x: 1, y: 1 }
    ];
    model.selections = [{ aggregate: 'none', alias: '', fieldResourceId: orderResourceId, id: 'selection:1', nodeId: 'orders' }];
    model.joins = [{ id: 'join:1', type: 'full', leftNodeId: 'orders', leftFieldResourceId: orderResourceId, rightNodeId: 'customers', rightFieldResourceId: statusResourceId }];

    expect(validateQueryModel(model, 'MySql').some((item) => item.message.includes('FULL JOIN'))).toBe(true);
    expect(validateQueryModel(model, 'Sqlite').some((item) => item.message.includes('FULL JOIN'))).toBe(true);
    expect(validateQueryModel(model, 'PostgreSQL').some((item) => item.message.includes('FULL JOIN'))).toBe(false);
  });
});
