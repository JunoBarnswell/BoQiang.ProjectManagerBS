import type {
  ApplicationDataSourceTableRowDeleteRequest,
  ApplicationDataSourceTableRowUpsertRequest
} from '../../api/application-data-center/applicationDataCenter.types';

export interface TableRowConcurrencyContext {
  concurrencyColumn?: string | null;
  primaryKeys: readonly string[];
}

export function buildTableRowUpdateRequest(
  row: Record<string, unknown>,
  values: Record<string, unknown>,
  context: TableRowConcurrencyContext
): ApplicationDataSourceTableRowUpsertRequest {
  const snapshot = buildTableRowSnapshot(row, context.primaryKeys);
  return {
    confirmed: true,
    expectedAffectedRows: 1,
    keyValues: snapshot.keyValues,
    originalValues: snapshot.originalValues,
    values,
    ...(readColumnValue(row, context.concurrencyColumn) !== undefined
      ? { versionValue: readColumnValue(row, context.concurrencyColumn) }
      : {})
  };
}

export function buildTableRowDeleteRequest(
  row: Record<string, unknown>,
  context: TableRowConcurrencyContext
): ApplicationDataSourceTableRowDeleteRequest {
  const snapshot = buildTableRowSnapshot(row, context.primaryKeys);
  return {
    confirmed: true,
    expectedAffectedRows: 1,
    keyValues: snapshot.keyValues,
    originalValues: snapshot.originalValues,
    ...(readColumnValue(row, context.concurrencyColumn) !== undefined
      ? { versionValue: readColumnValue(row, context.concurrencyColumn) }
      : {})
  };
}

export function buildTableRowInsertRequest(values: Record<string, unknown>): ApplicationDataSourceTableRowUpsertRequest {
  return {
    confirmed: true,
    values
  };
}

function buildTableRowSnapshot(row: Record<string, unknown>, primaryKeys: readonly string[]) {
  const primaryKeySet = new Set(primaryKeys.map((key) => key.toLowerCase()));
  const keyValues: Record<string, unknown> = {};
  const originalValues: Record<string, unknown> = {};

  for (const [fieldCode, value] of Object.entries(row)) {
    if (primaryKeySet.has(fieldCode.toLowerCase())) {
      keyValues[fieldCode] = value;
    } else {
      originalValues[fieldCode] = value;
    }
  }

  return { keyValues, originalValues };
}

function readColumnValue(row: Record<string, unknown>, columnName?: string | null): unknown {
  if (!columnName) {
    return undefined;
  }

  const matchingKey = Object.keys(row).find((key) => key.toLowerCase() === columnName.toLowerCase());
  return matchingKey ? row[matchingKey] : undefined;
}
