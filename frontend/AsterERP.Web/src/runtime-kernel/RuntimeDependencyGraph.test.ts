import { describe, expect, it } from 'vitest';

import { RuntimeDiagnostics } from './Diagnostics';
import { RuntimeDependencyGraph } from './RuntimeDependencyGraph';

describe('RuntimeDependencyGraph', () => {
  it('returns only nodes affected by a resource or scoped path change', () => {
    const graph = new RuntimeDependencyGraph();
    const diagnostics = new RuntimeDiagnostics();
    graph.compile(new Map([
      ['first', ['customer.name', 'page:locale']],
      ['second', ['orders.rows']],
      ['third', ['row:name']]
    ]), diagnostics);

    expect(graph.affected(['customer.name'])).toEqual(['first']);
    expect(graph.affected([{ key: 'page:locale', scope: 'page', path: 'locale' }])).toEqual(['first']);
    expect(graph.affected(['missing'])).toEqual([]);
    expect(diagnostics.all).toEqual([]);
  });

  it('reports dependency cycles and preserves the cycle members in diagnostic details', () => {
    const graph = new RuntimeDependencyGraph();
    const diagnostics = new RuntimeDiagnostics();
    graph.compile(new Map([
      ['a', ['b']],
      ['b', ['a']],
      ['c', ['a']]
    ]), diagnostics);

    expect(diagnostics.all).toEqual(expect.arrayContaining([
      expect.objectContaining({ code: 'cyclicDependency', details: expect.objectContaining({ nodes: expect.arrayContaining(['a', 'b']) }) })
    ]));
  });
});
