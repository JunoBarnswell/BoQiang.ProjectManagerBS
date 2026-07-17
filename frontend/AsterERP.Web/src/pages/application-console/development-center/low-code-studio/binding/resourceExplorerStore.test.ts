import { describe, expect, it } from 'vitest';

import { resourceIdFor } from './bindingTypes';
import { findResourceUsages, findUnresolvedResourceUsages, hasStableResource } from './resourceExplorerStore';

describe('stable binding resource identity', () => {
  it('is deterministic for the same source, model and path', () => {
    expect(resourceIdFor('variables', 'customer.name', 'Customer')).toBe('variables:Customer:customer.name');
    expect(resourceIdFor('variables', 'customer.name', 'Customer')).toBe(resourceIdFor('variables', 'customer.name', 'Customer'));
  });

  it('keeps root resources stable when the path is empty', () => {
    expect(resourceIdFor('currentRow', '')).toBe('currentRow:*');
  });

  it('accepts dynamic properties below a runtime current-row root', () => {
    expect(hasStableResource(new Set(['currentRow:*']), 'currentRow:id')).toBe(true);
    expect(hasStableResource(new Set(['currentRow:*']), 'currentRow:orderNo')).toBe(true);
    expect(hasStableResource(new Set(['currentRow:*']), 'form:orderNo')).toBe(false);
  });

  it('uses the same trimmed identity for catalog and binding references', () => {
    expect(resourceIdFor(' variables ', ' customer.name ', ' Customer ')).toBe('variables:Customer:customer.name');
  });

  it('finds canonical property and data-slot usages without duplicating cyclic objects', () => {
    const document: Record<string, unknown> = {
      elements: {
        first: { props: { value: { resourceId: 'variables:count', resourceType: 'variables' } } },
        second: { bindings: { data: { resourceId: 'variables:count', resourceType: 'variables' } } }
      }
    };
    document.self = document;

    expect(findResourceUsages(document, 'variables:count').map((usage) => usage.path)).toEqual([
      '$.elements.first.props.value',
      '$.elements.second.bindings.data'
    ]);
  });

  it('reports orphaned ResourceRefs before a resource is removed and exposes repair paths', () => {
    const document = {
      variables: [{ id: 'current', name: 'Current', valueType: 'string' }],
      elements: {
        root: { props: { text: { resourceId: 'variables:deleted', resourceType: 'variables', valueType: 'string' } } }
      }
    };
    expect(findUnresolvedResourceUsages(document)).toEqual([{ path: '$.elements.root.props.text', resourceId: 'variables:deleted' }]);
  });
});
