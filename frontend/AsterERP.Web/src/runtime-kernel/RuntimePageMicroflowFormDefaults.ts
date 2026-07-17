import type { RuntimeArtifact } from '../pages/application-console/development-center/low-code-studio/document/RuntimeArtifact';

/**
 * The form scope is a page contract, not a by-product of user input. Register
 * every field before an action is evaluated so an empty value is distinguishable
 * from a broken `form:*` reference.
 */
export function createRuntimePageFormValues(artifact: Pick<RuntimeArtifact, 'elements'>): Record<string, unknown> {
  const values: Record<string, unknown> = {};
  for (const element of Object.values(artifact.elements)) {
    const binding = isRecord(element.bindings?.data) ? element.bindings.data : {};
    const field = typeof binding.field === 'string' ? binding.field.trim() : '';
    if (field && !Object.prototype.hasOwnProperty.call(values, field)) values[field] = undefined;
  }
  return values;
}

export function mergeRuntimePageMicroflowFormDefaults(
  artifact: Pick<RuntimeArtifact, 'elements'>,
  currentFormValues: Record<string, unknown>,
  nextVariables: Record<string, unknown>,
  options: { overwrite?: boolean } = {}
): Record<string, unknown> {
  const result = { ...createRuntimePageFormValues(artifact), ...currentFormValues };
  for (const element of Object.values(artifact.elements)) {
    const binding = isRecord(element.bindings?.data) ? element.bindings.data : {};
    const field = typeof binding.field === 'string' ? binding.field.trim() : '';
    const resourceId = typeof binding.defaultResourceId === 'string' ? binding.defaultResourceId : typeof binding.resourceId === 'string' ? binding.resourceId : '';
    const path = microflowPath(resourceId);
    if (!field || !path) continue;
    const value = readPath(isRecord(nextVariables.microflows) ? nextVariables.microflows : nextVariables, path);
    if (value === undefined || value === null) continue;
    if (options.overwrite || isEmpty(result[field])) result[field] = value;
  }
  return result;
}

function microflowPath(resourceId: string): string | null {
  if (!resourceId.startsWith('microflow::')) return null;
  const path = resourceId.slice('microflow::'.length).trim();
  return path || null;
}

function readPath(value: unknown, path: string): unknown {
  return path.split('.').filter(Boolean).reduce<unknown>((current, key) => {
    if (!current || typeof current !== 'object' || Array.isArray(current)) return undefined;
    return (current as Record<string, unknown>)[key];
  }, value);
}

function isEmpty(value: unknown): boolean {
  return value === undefined || value === null || value === '';
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value && typeof value === 'object' && !Array.isArray(value));
}
