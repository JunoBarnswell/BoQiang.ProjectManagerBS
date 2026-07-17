import { RUNTIME_SCOPE_NAMES, type RuntimeScopeName } from './RuntimeTypes';

export type RuntimeBoundScopeName = Exclude<RuntimeScopeName, 'variables' | 'system'>;

export interface RuntimeScopeRecord {
  readonly id: string;
  readonly kind: RuntimeBoundScopeName;
  readonly parentId: string | null;
  readonly values: Record<string, unknown>;
}

export interface RuntimeScopeSnapshot {
  readonly scopes: Record<string, Record<string, unknown>>;
  readonly variables: Record<string, unknown>;
}

export class RuntimeScopeStore {
  private readonly records = new Map<string, RuntimeScopeRecord>();
  private sequence = 0;

  public create(kind: RuntimeBoundScopeName, values: Record<string, unknown> = {}, parentId: string | null = null): string {
    if (!(RUNTIME_SCOPE_NAMES as readonly string[]).includes(kind)) {
      throw new Error(`Unsupported runtime scope: ${kind}`);
    }
    if (parentId !== null && !this.records.has(parentId)) throw new Error(`Runtime scope parent does not exist: ${parentId}`);
    const id = `${kind}-${++this.sequence}`;
    this.records.set(id, { id, kind, parentId, values: cloneRecord(values) });
    return id;
  }

  public read(scopeId: string, path?: string): unknown {
    const record = this.require(scopeId);
    const local = readPath(record.values, path);
    const value = !hasPath(record.values, path) && record.parentId ? this.read(record.parentId, path) : local;
    return cloneValue(value);
  }

  public write(scopeId: string, path: string, value: unknown): void {
    const record = this.require(scopeId);
    writePath(record.values, path, cloneValue(value));
  }

  public inherit(scopeId: string): RuntimeScopeRecord | null {
    const record = this.require(scopeId);
    const parent = record.parentId ? this.records.get(record.parentId) : undefined;
    return parent ? cloneRecordEntity(parent) : null;
  }

  public destroy(scopeId: string): void {
    this.require(scopeId);
    for (const child of [...this.records.values()]) if (child.parentId === scopeId) this.destroy(child.id);
    this.records.delete(scopeId);
  }

  public get(scopeId: string): RuntimeScopeRecord | null {
    const record = this.records.get(scopeId);
    return record ? cloneRecordEntity(record) : null;
  }

  public snapshot(variables: Record<string, unknown> = {}): RuntimeScopeSnapshot {
    const scopes: Record<string, Record<string, unknown>> = {};
    for (const record of this.records.values()) scopes[record.kind] = mergeInherited(record, this.records);
    return { scopes, variables: cloneRecord(variables) };
  }

  private require(scopeId: string): RuntimeScopeRecord {
    const record = this.records.get(scopeId);
    if (!record) throw new Error(`Runtime scope does not exist: ${scopeId}`);
    return record;
  }
}

function mergeInherited(record: RuntimeScopeRecord, records: ReadonlyMap<string, RuntimeScopeRecord>): Record<string, unknown> {
  const parent = record.parentId ? records.get(record.parentId) : undefined;
  return { ...(parent ? mergeInherited(parent, records) : {}), ...cloneRecord(record.values) };
}

function readPath(root: Record<string, unknown>, path = ''): unknown {
  if (!path) return root;
  return path.split('.').filter(Boolean).reduce<unknown>((current, key) => {
    if (!current || typeof current !== 'object') return undefined;
    return (current as Record<string, unknown>)[key];
  }, root);
}

function hasPath(root: Record<string, unknown>, path = ''): boolean {
  if (!path) return true;
  let current: unknown = root;
  for (const key of path.split('.').filter(Boolean)) {
    if (!current || typeof current !== 'object' || !(key in (current as Record<string, unknown>))) return false;
    current = (current as Record<string, unknown>)[key];
  }
  return true;
}

function writePath(root: Record<string, unknown>, path: string, value: unknown): void {
  const keys = path.split('.').filter(Boolean);
  if (keys.length === 0) throw new Error('Runtime scope write path is required.');
  let current = root;
  for (const key of keys.slice(0, -1)) {
    const next = current[key];
    if (!next || typeof next !== 'object' || Array.isArray(next)) current[key] = {};
    current = current[key] as Record<string, unknown>;
  }
  current[keys[keys.length - 1]] = value;
}

function cloneRecord(value: Record<string, unknown>): Record<string, unknown> {
  return cloneValue(value) as Record<string, unknown>;
}

function cloneRecordEntity(record: RuntimeScopeRecord): RuntimeScopeRecord {
  return { id: record.id, kind: record.kind, parentId: record.parentId, values: cloneRecord(record.values) };
}

function cloneValue<T>(value: T, seen = new WeakMap<object, unknown>()): T {
  if (value === null || typeof value !== 'object') return value;
  if (value instanceof Date) return new Date(value.getTime()) as T;
  const existing = seen.get(value);
  if (existing) return existing as T;
  if (Array.isArray(value)) {
    const cloned: unknown[] = [];
    seen.set(value, cloned);
    for (const item of value) cloned.push(cloneValue(item, seen));
    return cloned as T;
  }
  const cloned: Record<string, unknown> = {};
  seen.set(value, cloned);
  for (const [key, nested] of Object.entries(value)) cloned[key] = cloneValue(nested, seen);
  return cloned as T;
}
