export type WorkbenchTab = 'mapping' | 'microflows' | 'tables' | 'views';

export interface WorkbenchTabDefinition {
  badge?: number;
  description: string;
  key: WorkbenchTab;
  title: string;
}

export function parseJsonObject(value?: string | null): Record<string, unknown> {
  if (!value?.trim()) {
    return {};
  }

  try {
    const parsed = JSON.parse(value) as unknown;
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed as Record<string, unknown> : {};
  } catch {
    return {};
  }
}

export function qualifiedTableName(
  tableOrSchema?: { schemaName?: string | null; tableName: string } | string | null,
  tableName?: string | null
): string {
  const table = typeof tableOrSchema === 'object' && tableOrSchema
    ? tableOrSchema.tableName.trim()
    : tableName?.trim() ?? String(tableOrSchema ?? '').trim();
  const schema = typeof tableOrSchema === 'object' && tableOrSchema
    ? tableOrSchema.schemaName?.trim()
    : null;
  return schema ? `${schema}.${table}` : table;
}

export function formatDate(value?: string | null): string {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}
