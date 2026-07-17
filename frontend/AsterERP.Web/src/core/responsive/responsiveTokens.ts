import type { DensityMode } from './breakpoint';
import { ERP_BREAKPOINTS, getDensity, getResponsiveGridColumns, getResponsiveModalWidth, getResponsiveSearchRows } from './breakpoint';

export interface ResponsiveTokens {
  cardRadius: number;
  contentGap: number;
  headerHeight: number;
  modalWidth: number;
  pagePadding: number;
  sectionGap: number;
  tableRowHeight: number;
  toolbarGap: number;
  toolbarHeight: number;
  density: DensityMode;
  formColumns: number;
  searchRows: number;
  modalMode: 'modal' | 'drawer' | 'fullscreen';
}

export function getResponsiveTokens(width: number): ResponsiveTokens {
  const density = getDensity(width);
  const isCompact = density === 'compact';
  const isComfortable = density === 'comfortable';

  return {
    cardRadius: isCompact ? 20 : isComfortable ? 28 : 24,
    contentGap: isCompact ? 12 : 16,
    headerHeight: isCompact ? 48 : isComfortable ? 64 : 56,
    modalWidth: getResponsiveModalWidth(width),
    pagePadding: isCompact ? 16 : isComfortable ? 24 : 20,
    sectionGap: isCompact ? 12 : 16,
    tableRowHeight: isCompact ? 40 : isComfortable ? 52 : 44,
    toolbarGap: isCompact ? 8 : 12,
    toolbarHeight: isCompact ? 44 : isComfortable ? 56 : 48,
    density,
    formColumns: getResponsiveGridColumns(width),
    searchRows: getResponsiveSearchRows(width),
    modalMode: width < ERP_BREAKPOINTS.md ? 'drawer' : 'modal'
  };
}

export function scaleResponsiveTokens(tokens: ResponsiveTokens, scaleRatio: number): ResponsiveTokens {
  return {
    ...tokens,
    cardRadius: Math.round(tokens.cardRadius * scaleRatio),
    contentGap: Math.round(tokens.contentGap * scaleRatio),
    headerHeight: Math.round(tokens.headerHeight * scaleRatio),
    modalWidth: Math.round(tokens.modalWidth * scaleRatio),
    pagePadding: Math.round(tokens.pagePadding * scaleRatio),
    sectionGap: Math.round(tokens.sectionGap * scaleRatio),
    tableRowHeight: Math.round(tokens.tableRowHeight * scaleRatio),
    toolbarGap: Math.round(tokens.toolbarGap * scaleRatio),
    toolbarHeight: Math.round(tokens.toolbarHeight * scaleRatio)
  };
}
