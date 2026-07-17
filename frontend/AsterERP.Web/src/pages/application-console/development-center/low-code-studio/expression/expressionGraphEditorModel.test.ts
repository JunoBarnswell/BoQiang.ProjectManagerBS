import { describe, expect, it } from 'vitest';

import type { ExpressionGraph } from './expressionGraph';
import { appendExpressionArgument, deleteExpressionNode, moveExpressionArgument, replaceExpressionNode } from './expressionGraphEditorModel';

const graph: ExpressionGraph = { root: { kind: 'functionCall', functionId: 'concat', valueType: 'string', args: [{ kind: 'literal', value: 'a', valueType: 'string' }, { kind: 'literal', value: 'b', valueType: 'string' }] } };

describe('expression graph editor model', () => {
  it('adds, reorders, replaces and deletes arbitrary nodes immutably', () => {
    const appended = appendExpressionArgument(graph, ['root']);
    expect(appended.root && 'args' in appended.root ? appended.root.args : []).toHaveLength(3);
    const moved = moveExpressionArgument(appended, ['root'], 0, 1);
    expect(moved.root && 'args' in moved.root && moved.root.args[0]?.kind === 'literal' ? moved.root.args[0].value : null).toBe('b');
    const replaced = replaceExpressionNode(moved, ['root', { args: 0 }], { kind: 'literal', value: 'x', valueType: 'string' });
    expect(replaced.root && 'args' in replaced.root && replaced.root.args[0]?.kind === 'literal' ? replaced.root.args[0].value : null).toBe('x');
    const deleted = deleteExpressionNode(replaced, ['root', { args: 0 }]);
    expect(deleted.root && 'args' in deleted.root ? deleted.root.args : []).toHaveLength(2);
    expect(graph.root && 'args' in graph.root && graph.root.args[0]?.kind === 'literal' ? graph.root.args[0].value : null).toBe('a');
  });
});
