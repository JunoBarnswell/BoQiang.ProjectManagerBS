import { describe, expect, it } from 'vitest';

import { resolveRuntimeValue } from './runtimeExpression';

describe('runtime ExpressionValue evaluation', () => {
  it('resolves a canonical resource expression without source/path compatibility fields', () => {
    expect(resolveRuntimeValue({
      version: 'latest',
      kind: 'functionCall',
      dataType: 'number',
      functionId: 'toNumber',
      args: [{ version: 'latest', kind: 'resourceRef', dataType: 'string', resourceId: 'variables:amount' }]
    }, { variables: { amount: '42' } })).toBe(42);
  });

  it('fails closed for retired expression and template shapes', () => {
    expect(() => resolveRuntimeValue({ source: 'variables', path: 'amount' }, { variables: { amount: 42 } }))
      .toThrow('Migrate this value to ExpressionValue');
    expect(() => resolveRuntimeValue('{{variables.amount}}', { variables: { amount: 42 } }))
      .toThrow('Legacy {{...}} runtime templates');
  });
});
