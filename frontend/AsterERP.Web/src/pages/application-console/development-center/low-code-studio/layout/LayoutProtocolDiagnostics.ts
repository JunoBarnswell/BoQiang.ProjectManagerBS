export interface LayoutProtocolDiagnostic {
  code: string;
  path: string;
  args?: Record<string, string | number>;
}

export const layoutProtocolDiagnosticCodes = {
  invalidPlacementKind: 'LAYOUT_PLACEMENT_KIND_INVALID',
  multiplePlacementPayloads: 'LAYOUT_PLACEMENT_PAYLOAD_CONFLICT',
  missingAbsolutePlacement: 'LAYOUT_ABSOLUTE_PLACEMENT_REQUIRED',
  invalidContainerPayload: 'LAYOUT_CONTAINER_PAYLOAD_INVALID',
  invalidDimension: 'LAYOUT_DIMENSION_INVALID',
  invalidSizeRange: 'LAYOUT_SIZE_RANGE_INVALID',
  invalidAspectRatio: 'LAYOUT_ASPECT_RATIO_INVALID',
  invalidGridSpan: 'LAYOUT_GRID_SPAN_INVALID',
  missingConstraintStrategy: 'LAYOUT_CONSTRAINT_STRATEGY_REQUIRED',
  invalidMigrationAnchor: 'LAYOUT_MIGRATION_ANCHOR_INVALID',
  invalidMigrationState: 'LAYOUT_MIGRATION_STATE_INVALID'
} as const;
