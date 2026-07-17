export type LayoutMode = 'free' | 'flex' | 'grid' | 'constraints';

export type Dimension = number | `${number}%` | `${number}px` | 'auto' | 'min-content' | 'max-content' | 'fit-content';

export interface ElementSizeSpec {
  width: Dimension;
  height: Dimension;
  minWidth?: Dimension;
  maxWidth?: Dimension;
  minHeight?: Dimension;
  maxHeight?: Dimension;
  aspectRatio?: number;
}

export interface FlexContainerSpec {
  direction: 'row' | 'row-reverse' | 'column' | 'column-reverse';
  wrap: 'nowrap' | 'wrap' | 'wrap-reverse';
  gap: number;
  alignItems: 'start' | 'center' | 'end' | 'stretch' | 'baseline';
  justifyContent: 'start' | 'center' | 'end' | 'space-between' | 'space-around' | 'space-evenly';
}

export interface GridContainerSpec {
  columns: string[];
  rows: string[];
  columnGap: number;
  rowGap: number;
  autoFlow: 'row' | 'column' | 'dense' | 'row-dense' | 'column-dense';
}

export interface ConstraintContainerSpec {
  coordinateSpace: 'parent-padding-box';
}

export interface ContainerLayoutSpec {
  mode: LayoutMode;
  flex?: FlexContainerSpec;
  grid?: GridContainerSpec;
  constraints?: ConstraintContainerSpec;
}

export interface AbsolutePlacement {
  x: number;
  y: number;
  zIndex?: number;
}

export interface FlexItemPlacement {
  order: number;
  grow: number;
  shrink: number;
  basis: Dimension;
  alignSelf?: 'auto' | 'start' | 'center' | 'end' | 'stretch' | 'baseline';
}

export interface GridItemPlacement {
  rowStart: number | 'auto';
  rowSpan: number;
  columnStart: number | 'auto';
  columnSpan: number;
  alignSelf?: 'auto' | 'start' | 'center' | 'end' | 'stretch';
  justifySelf?: 'auto' | 'start' | 'center' | 'end' | 'stretch';
}

export interface ConstraintPlacement {
  left?: number;
  right?: number;
  top?: number;
  bottom?: number;
  centerX?: number;
  centerY?: number;
  stretchX?: boolean;
  stretchY?: boolean;
}

export interface LayoutMigrationAnchor {
  coordinateSpace: 'parent-padding-box';
  rect: {
    x: number;
    y: number;
    width: number;
    height: number;
  };
  sequence: number;
}

export interface LayoutMigrationState {
  previousContainers?: Partial<Record<LayoutMode, ContainerLayoutSpec>>;
  previousPlacements?: Partial<Record<LayoutMode, ChildPlacementSpec>>;
}

export interface ChildPlacementSpec {
  kind: 'absolute' | 'flex-item' | 'grid-item' | 'constrained';
  absolute?: AbsolutePlacement;
  flexItem?: FlexItemPlacement;
  gridItem?: GridItemPlacement;
  constrained?: ConstraintPlacement;
}

export interface LayoutProtocol {
  container: ContainerLayoutSpec;
  placement: ChildPlacementSpec;
  size: ElementSizeSpec;
  /** Resolved geometry retained only while migrating between container modes. */
  anchor?: LayoutMigrationAnchor;
  /** Container configurations retained per mode so a mode round-trip is lossless. */
  migration?: LayoutMigrationState;
}

export function defaultContainerLayout(mode: LayoutMode = 'free'): ContainerLayoutSpec {
  if (mode === 'flex') return { mode, flex: { direction: 'row', wrap: 'nowrap', gap: 0, alignItems: 'stretch', justifyContent: 'start' } };
  if (mode === 'grid') return { mode, grid: { columns: ['1fr'], rows: ['auto'], columnGap: 0, rowGap: 0, autoFlow: 'row' } };
  if (mode === 'constraints') return { mode, constraints: { coordinateSpace: 'parent-padding-box' } };
  return { mode };
}

export function defaultPlacement(mode: LayoutMode, x = 0, y = 0): ChildPlacementSpec {
  if (mode === 'flex') return { kind: 'flex-item', flexItem: { order: 0, grow: 0, shrink: 1, basis: 'auto' } };
  if (mode === 'grid') return { kind: 'grid-item', gridItem: { rowStart: 'auto', rowSpan: 1, columnStart: 'auto', columnSpan: 1 } };
  if (mode === 'constraints') return { kind: 'constrained', constrained: { left: x, top: y } };
  return { kind: 'absolute', absolute: { x, y } };
}

export function normalizeLayoutProtocol(protocol: LayoutProtocol): LayoutProtocol {
  const mode = protocol.container.mode;
  const container = defaultContainerLayout(mode);
  const placement = defaultPlacement(mode, protocol.placement.absolute?.x, protocol.placement.absolute?.y);
  return {
    container: mode === 'flex' ? { ...container, flex: { ...container.flex!, ...protocol.container.flex } } : mode === 'grid' ? { ...container, grid: { ...container.grid!, ...protocol.container.grid } } : mode === 'constraints' ? { ...container, constraints: { ...container.constraints!, ...protocol.container.constraints } } : container,
    placement: mode === 'free' ? { kind: 'absolute', absolute: { x: protocol.placement.absolute?.x ?? 0, y: protocol.placement.absolute?.y ?? 0, ...(protocol.placement.absolute?.zIndex === undefined ? {} : { zIndex: protocol.placement.absolute.zIndex }) } } : placement,
    size: { ...protocol.size },
    ...(protocol.anchor ? { anchor: protocol.anchor } : {}),
    ...(protocol.migration ? { migration: protocol.migration } : {})
  };
}
