import type { ReactNode } from 'react';

import { ResponsivePageContent } from './ResponsivePageContent';
import { ResponsivePageHeader } from './ResponsivePageHeader';

interface ResponsivePageProps {
  actions?: ReactNode;
  children: ReactNode;
  className?: string;
  description?: ReactNode;
  eyebrow?: ReactNode;
  footer?: ReactNode;
  fitScreen?: boolean;
  searchArea?: ReactNode;
  title: ReactNode;
  toolbar?: ReactNode;
}

export function ResponsivePage({
  actions,
  children,
  className,
  description,
  eyebrow,
  footer,
  fitScreen = true,
  searchArea,
  title,
  toolbar
}: ResponsivePageProps) {
  return (
    <div className={['flex flex-1 h-full min-h-0 flex-col gap-[var(--erp-section-gap)] responsive-page', className ?? ''].filter(Boolean).join(' ')}>
      <ResponsivePageHeader actions={actions} description={description} eyebrow={eyebrow} title={title} />

      <div className="flex flex-1 min-h-0 flex-col">
        <div className="list-page-card flex flex-1 min-h-0 flex-col">
          {searchArea ? <section className="responsive-page-search">{searchArea}</section> : null}
          {toolbar ? <section className="responsive-page-toolbar">{toolbar}</section> : null}
          <ResponsivePageContent minHeight={fitScreen ? 0 : undefined}>{children}</ResponsivePageContent>
        </div>
      </div>

      {footer ? <div className="flex flex-wrap justify-end gap-[10px] pt-[2px]">{footer}</div> : null}
    </div>
  );
}

