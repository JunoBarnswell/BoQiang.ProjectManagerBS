export const ERP_BREAKPOINTS = {
  xs: 0,
  sm: 768,
  md: 1024,
  lg: 1366,
  xl: 1600,
  xxl: 1920,
  ultra: 2560
} as const;

export const ERP_BREAKPOINT_ORDER = ['xs', 'sm', 'md', 'lg', 'xl', 'xxl', 'ultra'] as const;

export type BreakpointName = (typeof ERP_BREAKPOINT_ORDER)[number];

export type DensityMode = 'compact' | 'standard' | 'comfortable';

export const BREAKPOINT_SEQUENCE: BreakpointName[] = [...ERP_BREAKPOINT_ORDER];

export function getBreakpoint(width: number): BreakpointName {
  if (width >= ERP_BREAKPOINTS.ultra) {
    return 'ultra';
  }

  if (width >= ERP_BREAKPOINTS.xxl) {
    return 'xxl';
  }

  if (width >= ERP_BREAKPOINTS.xl) {
    return 'xl';
  }

  if (width >= ERP_BREAKPOINTS.lg) {
    return 'lg';
  }

  if (width >= ERP_BREAKPOINTS.md) {
    return 'md';
  }

  if (width >= ERP_BREAKPOINTS.sm) {
    return 'sm';
  }

  return 'xs';
}

export function isBreakpointAtLeast(current: BreakpointName, target: BreakpointName) {
  return BREAKPOINT_SEQUENCE.indexOf(current) >= BREAKPOINT_SEQUENCE.indexOf(target);
}

export function isBreakpointBelow(current: BreakpointName, target: BreakpointName) {
  return BREAKPOINT_SEQUENCE.indexOf(current) < BREAKPOINT_SEQUENCE.indexOf(target);
}

export function getDensity(width: number): DensityMode {
  if (width < ERP_BREAKPOINTS.lg) {
    return 'compact';
  }

  if (width >= ERP_BREAKPOINTS.xxl) {
    return 'comfortable';
  }

  return 'standard';
}

export function getResponsiveGridColumns(width: number) {
  if (width < ERP_BREAKPOINTS.sm) {
    return 1;
  }

  if (width < ERP_BREAKPOINTS.md) {
    return 2;
  }

  if (width < ERP_BREAKPOINTS.xl) {
    return 3;
  }

  if (width < ERP_BREAKPOINTS.xxl) {
    return 4;
  }

  if (width < ERP_BREAKPOINTS.ultra) {
    return 5;
  }

  return 6;
}

export function getResponsiveSearchRows(width: number) {
  if (width < ERP_BREAKPOINTS.sm) {
    return 1;
  }

  if (width < ERP_BREAKPOINTS.lg) {
    return 1;
  }

  if (width < ERP_BREAKPOINTS.xxl) {
    return 2;
  }

  return 3;
}

export function getResponsiveToolbarVisibleCount(width: number, actionCount: number) {
  if (actionCount <= 2) {
    return actionCount;
  }

  if (width < ERP_BREAKPOINTS.sm) {
    return 1;
  }

  if (width < ERP_BREAKPOINTS.md) {
    return Math.min(2, actionCount);
  }

  if (width < ERP_BREAKPOINTS.xl) {
    return Math.min(3, actionCount);
  }

  if (width < ERP_BREAKPOINTS.xxl) {
    return Math.min(4, actionCount);
  }

  return actionCount;
}

export type ResponsiveModalMode = 'auto' | 'modal' | 'drawer' | 'fullscreen';

export function getResponsiveModalMode(width: number, mode: ResponsiveModalMode): 'modal' | 'drawer' | 'fullscreen' {
  if (mode !== 'auto') {
    return mode;
  }

  if (width < ERP_BREAKPOINTS.sm) {
    return 'fullscreen';
  }

  if (width < ERP_BREAKPOINTS.md) {
    return 'drawer';
  }

  return 'modal';
}

export function getResponsiveModalWidth(width: number) {
  if (width >= ERP_BREAKPOINTS.ultra) {
    return 1120;
  }

  if (width >= ERP_BREAKPOINTS.xxl) {
    return 960;
  }

  if (width >= ERP_BREAKPOINTS.xl) {
    return 840;
  }

  return 720;
}

