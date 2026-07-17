import type { Dimension, LayoutMode, LayoutProtocol } from './LayoutProtocol';
import { layoutProtocolDiagnosticCodes, type LayoutProtocolDiagnostic } from './LayoutProtocolDiagnostics';

export function validateLayoutProtocol(protocol: LayoutProtocol): LayoutProtocolDiagnostic[] {
  const diagnostics: LayoutProtocolDiagnostic[] = [];
  const { container, placement, size } = protocol;
  const payloadCount = [placement.absolute, placement.flexItem, placement.gridItem, placement.constrained].filter(Boolean).length;
  if (payloadCount !== 1) diagnostics.push({ code: layoutProtocolDiagnosticCodes.multiplePlacementPayloads, path: 'placement' });
  const expected = container.mode === 'free' ? 'absolute' : container.mode === 'flex' ? 'flex-item' : container.mode === 'grid' ? 'grid-item' : 'constrained';
  if (placement.kind !== expected) diagnostics.push({ code: layoutProtocolDiagnosticCodes.invalidPlacementKind, path: 'placement.kind', args: { expected } });
  if (container.mode === 'free' && !placement.absolute) diagnostics.push({ code: layoutProtocolDiagnosticCodes.missingAbsolutePlacement, path: 'placement.absolute' });
  if (container.mode === 'flex' && (!container.flex || !Number.isFinite(container.flex.gap))) diagnostics.push({ code: layoutProtocolDiagnosticCodes.invalidContainerPayload, path: 'container.flex' });
  if (container.mode === 'grid' && (!container.grid || container.grid.columns.length === 0 || container.grid.rows.length === 0)) diagnostics.push({ code: layoutProtocolDiagnosticCodes.invalidContainerPayload, path: 'container.grid' });
  if (container.mode === 'constraints' && container.constraints?.coordinateSpace !== 'parent-padding-box') diagnostics.push({ code: layoutProtocolDiagnosticCodes.missingConstraintStrategy, path: 'container.constraints.coordinateSpace' });
  for (const [key, value] of Object.entries(size)) if (value !== undefined && !isDimension(value)) diagnostics.push({ code: layoutProtocolDiagnosticCodes.invalidDimension, path: `size.${key}` });
  if (size.aspectRatio !== undefined && (!Number.isFinite(size.aspectRatio) || size.aspectRatio <= 0)) diagnostics.push({ code: layoutProtocolDiagnosticCodes.invalidAspectRatio, path: 'size.aspectRatio' });
  if (placement.gridItem && (!isPositiveInteger(placement.gridItem.rowSpan) || !isPositiveInteger(placement.gridItem.columnSpan))) diagnostics.push({ code: layoutProtocolDiagnosticCodes.invalidGridSpan, path: 'placement.gridItem' });
  if (size.minWidth !== undefined && size.maxWidth !== undefined && comparable(size.minWidth, size.maxWidth) && Number(size.minWidth) > Number(size.maxWidth)) diagnostics.push({ code: layoutProtocolDiagnosticCodes.invalidSizeRange, path: 'size.width' });
  if (protocol.anchor) {
    if (protocol.anchor.coordinateSpace !== 'parent-padding-box') diagnostics.push({ code: layoutProtocolDiagnosticCodes.invalidMigrationAnchor, path: 'anchor.coordinateSpace' });
    const rect = protocol.anchor.rect;
    if (!rect || ![rect.x, rect.y, rect.width, rect.height].every((value) => Number.isFinite(value)) || rect.width <= 0 || rect.height <= 0) diagnostics.push({ code: layoutProtocolDiagnosticCodes.invalidMigrationAnchor, path: 'anchor.rect' });
    if (!Number.isInteger(protocol.anchor.sequence) || protocol.anchor.sequence < 0) diagnostics.push({ code: layoutProtocolDiagnosticCodes.invalidMigrationAnchor, path: 'anchor.sequence' });
  }
  validateMigrationState(protocol, diagnostics);
  return diagnostics;
}

function validateMigrationState(protocol: LayoutProtocol, diagnostics: LayoutProtocolDiagnostic[]): void {
  const migration = protocol.migration as unknown as Record<string, unknown> | undefined;
  if (!migration) return;
  for (const [mode, container] of Object.entries(asRecord(migration.previousContainers))) {
    if (!isLayoutMode(mode) || !isValidContainerForMode(container, mode)) diagnostics.push({ code: layoutProtocolDiagnosticCodes.invalidMigrationState, path: `migration.previousContainers.${mode}` });
  }
  for (const [mode, placement] of Object.entries(asRecord(migration.previousPlacements))) {
    if (!isLayoutMode(mode) || !isValidPlacementForMode(placement, mode)) diagnostics.push({ code: layoutProtocolDiagnosticCodes.invalidMigrationState, path: `migration.previousPlacements.${mode}` });
  }
}

function isValidContainerForMode(value: unknown, mode: LayoutMode): boolean {
  const container = asRecord(value);
  if (container.mode !== mode) return false;
  if (mode === 'flex') {
    const flex = asRecord(container.flex);
    return Number.isFinite(flex.gap);
  }
  if (mode === 'grid') {
    const grid = asRecord(container.grid);
    return Array.isArray(grid.columns) && grid.columns.length > 0 && Array.isArray(grid.rows) && grid.rows.length > 0;
  }
  return mode !== 'constraints' || asRecord(container.constraints).coordinateSpace === 'parent-padding-box';
}

function isValidPlacementForMode(value: unknown, mode: LayoutMode): boolean {
  const placement = asRecord(value);
  const payloadKey = mode === 'free' ? 'absolute' : mode === 'flex' ? 'flexItem' : mode === 'grid' ? 'gridItem' : 'constrained';
  return placement.kind === (mode === 'free' ? 'absolute' : mode === 'flex' ? 'flex-item' : mode === 'grid' ? 'grid-item' : 'constrained') && isRecord(placement[payloadKey]);
}

function isLayoutMode(value: string): value is LayoutMode { return value === 'free' || value === 'flex' || value === 'grid' || value === 'constraints'; }
function isRecord(value: unknown): boolean { return value !== null && typeof value === 'object' && !Array.isArray(value); }
function asRecord(value: unknown): Record<string, unknown> { return value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : {}; }

function isDimension(value: unknown): value is Dimension {
  return value === 'auto' || value === 'min-content' || value === 'max-content' || value === 'fit-content' || (typeof value === 'number' && Number.isFinite(value) && value >= 0) || (typeof value === 'string' && /^(?:\d+(?:\.\d+)?)(?:%|px)$/.test(value));
}

function comparable(left: Dimension, right: Dimension): boolean { return typeof left === 'number' && typeof right === 'number'; }
function isPositiveInteger(value: unknown): value is number { return typeof value === 'number' && Number.isFinite(value) && Number.isInteger(value) && value > 0; }
