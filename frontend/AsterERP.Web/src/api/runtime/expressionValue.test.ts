import { describe, expect, it } from 'vitest';

import { expressionValueCanonicalJson, expressionValueFromGraph } from './expressionValue';

describe('latest ExpressionValue contract', () => {
  it('converts a graph to a stable AST with sorted dependencies', () => {
    const expression = expressionValueFromGraph({
      root: {
        kind: 'object',
        valueType: 'object',
        properties: {
          z: { kind: 'resourceRef', valueType: 'string', resourceId: 'form:z' },
          a: { kind: 'resourceRef', valueType: 'string', resourceId: 'form:a' }
        }
      }
    }, 'object');

    expect(expression.version).toBe('latest');
    expect(expression.dependencies).toEqual(['form:a', 'form:z']);
    expect(expressionValueCanonicalJson(expression).indexOf('"a"')).toBeLessThan(expressionValueCanonicalJson(expression).indexOf('"z"'));
  });
});
