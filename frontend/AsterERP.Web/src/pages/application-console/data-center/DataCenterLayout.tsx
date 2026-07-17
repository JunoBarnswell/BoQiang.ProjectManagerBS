import type { ReactNode } from 'react';

interface DataCenterLayoutProps {
  detail: ReactNode;
  density?: 'compact' | 'tight';
  table: ReactNode;
  typeTree: ReactNode;
}

export function DataCenterLayout({ density = 'compact', detail, table, typeTree }: DataCenterLayoutProps) {
  const tight = density === 'tight';

  return (
    <div className={`grid h-full min-h-0 overflow-hidden max-lg:grid-cols-1 ${tight ? 'grid-cols-[188px_minmax(0,1fr)_268px] gap-1.5 max-xl:grid-cols-[176px_minmax(0,1fr)]' : 'grid-cols-[210px_minmax(0,1fr)_296px] gap-2 max-xl:grid-cols-[196px_minmax(0,1fr)]'}`}>
      <div className="min-h-0 max-lg:hidden">{typeTree}</div>
      <div className="min-h-0 overflow-hidden">{table}</div>
      <div className="min-h-0 overflow-y-auto max-xl:col-span-2 max-lg:col-span-1">{detail}</div>
    </div>
  );
}
