import type { CSSProperties } from 'react';

import { useAutoHeight } from '../../core/responsive/useAutoHeight';

import { DataTable } from './DataTable';
import type { DataTableProps } from './tableTypes';

interface AutoHeightTableProps<TItem> extends DataTableProps<TItem> {
  reservedHeight?: number;
}

export function AutoHeightTable<TItem>({ reservedHeight = 0, ...props }: AutoHeightTableProps<TItem>) {
  const { style } = useAutoHeight({
    enabled: reservedHeight > 0,
    minHeight: 320,
    offset: reservedHeight
  });

  return <DataTable {...props} fitScreen style={style as CSSProperties} className={`auto-height-table ${props.className ?? ''}`.trim()} />;
}
