import { describe, expect, it } from 'vitest';

import { getExpressionFunction, inferExpressionFunctionType, listExpressionFunctions } from './expressionFunctionCatalog';

describe('expression function catalog', () => {
  it('exposes functions and operators with typed arity', () => {
    expect(listExpressionFunctions('operator').some((item) => item.name === 'equals')).toBe(true);
    expect(getExpressionFunction('concat')?.minArgs).toBe(1);
    expect(inferExpressionFunctionType('coalesce', [{ valueType: 'number' }])).toBe('number');
  });
});
