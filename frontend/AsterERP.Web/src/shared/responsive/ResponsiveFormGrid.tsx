import type { CSSProperties, ReactNode } from 'react';

import { getResponsiveGridColumns, type BreakpointName } from '../../core/responsive/breakpoint';
import { useBreakpoint } from '../../core/responsive/useBreakpoint';
import { useViewportSize } from '../../core/responsive/useViewportSize';

export type ResponsiveGridColumns = number | Partial<Record<BreakpointName, number>>;

interface ResponsiveFormGridProps {
  children: ReactNode;
  className?: string;
  columns?: ResponsiveGridColumns;
  dense?: boolean;
}

function resolveColumns(columns: ResponsiveGridColumns | undefined, breakpoint: BreakpointName, width: number) {
  if (typeof columns === 'number') {
    return columns;
  }

  if (!columns) {
    return getResponsiveGridColumns(width);
  }

  const orderedBreakpoints: BreakpointName[] = ['ultra', 'xxl', 'xl', 'lg', 'md', 'sm', 'xs'];
  const currentIndex = orderedBreakpoints.indexOf(breakpoint);

  for (let index = currentIndex; index < orderedBreakpoints.length; index += 1) {
    const token = orderedBreakpoints[index];
    const nextColumns = columns[token];

    if (typeof nextColumns === 'number') {
      return nextColumns;
    }
  }

  return getResponsiveGridColumns(width);
}

export function ResponsiveFormGrid({ children, className, columns, dense = false }: ResponsiveFormGridProps) {
  const { breakpoint } = useBreakpoint();
  const { width } = useViewportSize();
  const resolvedColumns = typeof columns === 'number' ? columns : resolveColumns(columns, breakpoint, width);
  const classNames = ['responsive-form-grid', dense ? 'responsive-form-grid--dense' : '', className ?? '']
    .filter(Boolean)
    .join(' ');

  const style = {
    ['--responsive-form-columns' as string]: resolvedColumns
  } as CSSProperties;

  return (
    <div className={classNames} style={style}>
      {children}
    </div>
  );
}
