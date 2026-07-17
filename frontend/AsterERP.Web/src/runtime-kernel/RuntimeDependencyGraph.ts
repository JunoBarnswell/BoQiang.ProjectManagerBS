import { RuntimeDiagnostics } from './Diagnostics';

export interface RuntimeDependencyChange {
  readonly key: string;
  readonly scope?: string;
  readonly path?: string;
}

export interface RuntimeDependencyCycle {
  readonly nodes: readonly string[];
}

export class RuntimeDependencyGraph {
  private readonly dependents = new Map<string, Set<string>>();
  private readonly dependencies = new Map<string, Set<string>>();
  private readonly nodeOrder: string[] = [];

  public compile(entries: ReadonlyMap<string, readonly string[]>, diagnostics: RuntimeDiagnostics): void {
    this.dependents.clear();
    this.dependencies.clear();
    this.nodeOrder.length = 0;
    for (const [nodeId, keys] of entries) {
      this.nodeOrder.push(nodeId);
      const dependencies = new Set(keys);
      this.dependencies.set(nodeId, dependencies);
      for (const key of dependencies) {
        const nodes = this.dependents.get(key) ?? new Set<string>();
        nodes.add(nodeId);
        this.dependents.set(key, nodes);
      }
    }
    this.detectCycles(diagnostics);
  }

  public affected(changes: readonly RuntimeDependencyChange[] | readonly string[]): readonly string[] {
    const keys = changes.map((change) => typeof change === 'string' ? change : dependencyKeys(change)).flat();
    const affected = new Set<string>();
    for (const key of keys) for (const nodeId of this.dependents.get(key) ?? []) affected.add(nodeId);
    return this.nodeOrder.filter((nodeId) => affected.has(nodeId));
  }

  public dependenciesFor(nodeId: string): readonly string[] {
    return [...(this.dependencies.get(nodeId) ?? [])];
  }

  private detectCycles(diagnostics: RuntimeDiagnostics): void {
    const visiting = new Set<string>();
    const visited = new Set<string>();
    const visit = (nodeId: string, path: string[]): void => {
      if (visiting.has(nodeId)) {
        const cycle = [...path.slice(path.indexOf(nodeId)), nodeId];
        diagnostics.error('cyclicDependency', `Runtime dependency graph contains a cycle: ${cycle.join(' -> ')}`, `dependencies.${nodeId}`, { nodes: cycle });
        return;
      }
      if (visited.has(nodeId)) return;
      visiting.add(nodeId);
      for (const dependency of this.dependencies.get(nodeId) ?? []) if (this.dependencies.has(dependency)) visit(dependency, [...path, nodeId]);
      visiting.delete(nodeId);
      visited.add(nodeId);
    };
    for (const nodeId of this.nodeOrder) visit(nodeId, []);
  }
}

function dependencyKeys(change: RuntimeDependencyChange): readonly string[] {
  const keys = [change.key];
  if (change.scope) keys.push(`${change.scope}:${change.path ?? ''}`);
  if (change.path) keys.push(`${change.scope ?? ''}:${change.path}`);
  return keys;
}
