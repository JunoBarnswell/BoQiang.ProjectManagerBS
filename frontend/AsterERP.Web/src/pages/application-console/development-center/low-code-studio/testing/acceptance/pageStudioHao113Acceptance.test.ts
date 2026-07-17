import { describe, expect, it } from 'vitest';

import { buildQueryPlanRequest, createInitialQueryModel } from '../../../../data-center/query-model/queryModelModel';
import { formatWorkbenchSql } from '../../../../data-center/workbench/components/WorkbenchSqlEditor';

describe('HAO-113 Data Studio product acceptance boundary', () => {
  it('compiles structured Query Model without raw SQL fallback', () => {
    const model = createInitialQueryModel('source-1');
    model.nodes = [{ id: 'node:orders', alias: 'o', columns: [], kind: 'table', name: 'orders', resourceId: 'data:table:orders', x: 0, y: 0 }];
    const request = buildQueryPlanRequest(model);
    expect(request).not.toHaveProperty('rawSql');
    expect(request.nodes[0].resourceId).toBe('data:table:orders');
    expect(request).not.toHaveProperty('objectName');
  });

  it('formats SQL through the current Workbench editor contract', () => {
    expect(formatWorkbenchSql('select id,name from orders where id = 1 and name = 2')).toContain('\nfrom orders');
  });

  it('retains explicit typed query parameters in the provider request', () => {
    const model = createInitialQueryModel('source-1');
    model.nodes = [{ id: 'node:orders', alias: 'o', columns: [], kind: 'table', name: 'orders', resourceId: 'data:table:orders', x: 0, y: 0 }];
    model.parameters = [{ resourceId: 'query-parameter:customer-id', name: 'customerId', type: 'string', value: 'a' }];
    expect(buildQueryPlanRequest(model).parameters).toEqual(expect.arrayContaining([expect.objectContaining({ name: 'customerId', type: 'string' })]));
  });
});
