function shouldFlatten(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}

function flattenObject(
  target: Record<string, unknown>,
  source: Record<string, unknown>,
  prefix: string,
  depth: number
) {
  if (depth > 4) {
    return;
  }

  Object.entries(source).forEach(([key, value]) => {
    const path = prefix ? `${prefix}.${key}` : key;
    target[path] = value;

    if (shouldFlatten(value)) {
      flattenObject(target, value, path, depth + 1);
    }
  });
}

export function expandPrintRuntimeVariables(data: Record<string, unknown> | null | undefined): Record<string, unknown> {
  const expanded: Record<string, unknown> = {};
  if (!data) {
    return expanded;
  }

  flattenObject(expanded, data, '', 0);
  return expanded;
}
