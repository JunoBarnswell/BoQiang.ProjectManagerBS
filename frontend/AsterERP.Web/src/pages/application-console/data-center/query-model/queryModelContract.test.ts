import { describe, expect, it } from 'vitest';

import { buildQueryPlanRequest, createInitialQueryModel } from './queryModelModel';

describe('query model contract boundary', () => {
  it('keeps structured Resource ID fields in config while creating a typed preview request', () => {
    const model = createInitialQueryModel('source-1');
    const tableResourceId = 'data:table:orders';
    const fieldResourceId = `${tableResourceId}:column:customer-id`;
    model.nodes = [{ id: 'node:source-1:orders', alias: 'o', columns: [{ resourceId: fieldResourceId, columnName: 'customer_id', dataType: 'TEXT', nullable: false, order: 1, primaryKey: false }], kind: 'table', name: 'orders', resourceId: tableResourceId, x: 80, y: 80 }];
    model.groupBy = [{ fieldResourceId, nodeId: 'node:source-1:orders' }];
    model.having = [{ fieldResourceId, id: 'having:1', nodeId: 'node:source-1:orders', operator: 'gt', parameterResourceId: 'query-parameter:minimum' }];
    const request = buildQueryPlanRequest(model);
    expect(request).not.toHaveProperty('rawSql');
    expect(request).not.toHaveProperty('objectName');
    expect(request.nodes[0].resourceId).toBe(tableResourceId);
    expect(model.groupBy).toEqual([{ fieldResourceId, nodeId: 'node:source-1:orders' }]);
  });
});
