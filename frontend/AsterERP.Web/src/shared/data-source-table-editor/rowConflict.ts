import type { ApplicationDataSourceTableRowMutationResponse } from '../../api/application-data-center/applicationDataCenter.types';

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

export function readRowConflictPayload(value: unknown): ApplicationDataSourceTableRowMutationResponse | null {
  if (!isRecord(value) || value.conflict !== true) return null;
  return {
    affectedRows: typeof value.affectedRows === 'number' ? value.affectedRows : 0,
    auditId: typeof value.auditId === 'string' ? value.auditId : null,
    canOverwrite: value.canOverwrite === true,
    canRetry: value.canRetry === true,
    conflict: true,
    conflictMessage: typeof value.conflictMessage === 'string' ? value.conflictMessage : null,
    executionStatus: typeof value.executionStatus === 'string' ? value.executionStatus : null,
    localValues: isRecord(value.localValues) ? value.localValues : undefined,
    ledgerId: typeof value.ledgerId === 'string' ? value.ledgerId : null,
    recoveryRequired: value.recoveryRequired === true,
    requestHash: typeof value.requestHash === 'string' ? value.requestHash : null,
    serverValues: isRecord(value.serverValues) ? value.serverValues : undefined,
    succeeded: value.succeeded === true
  };
}
