import { describe, expect, it } from 'vitest';

import { queryKeys } from '../../core/query/queryKeys';
import { getScopedQueryKey } from '../dashboard/dashboardModel';

describe('system management query scope', () => {
  it('keeps department and position lookups isolated by workspace', () => {
    const departmentsForMes = getScopedQueryKey(
      queryKeys.systemManagement.departments(1, 500, '', 'Enabled', ''),
      'tenant-a:MES'
    );
    const departmentsForWms = getScopedQueryKey(
      queryKeys.systemManagement.departments(1, 500, '', 'Enabled', ''),
      'tenant-a:WMS'
    );
    const positionsForMes = getScopedQueryKey(
      queryKeys.systemManagement.positions(1, 500, '', 'Enabled', ''),
      'tenant-a:MES'
    );

    expect(departmentsForMes).not.toEqual(departmentsForWms);
    expect(departmentsForMes).not.toEqual(positionsForMes);
    expect(departmentsForMes.at(-2)).toBe('system');
    expect(departmentsForMes.at(-1)).toBe('tenant-a:MES');
  });
});
