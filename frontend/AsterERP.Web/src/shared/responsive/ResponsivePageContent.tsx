import type { CSSProperties, ReactNode } from 'react';

interface ResponsivePageContentProps {
  children: ReactNode;
  className?: string;
  minHeight?: number;
}

export function ResponsivePageContent({ children, className, minHeight }: ResponsivePageContentProps) {
  const style = (minHeight ? { minHeight: `${minHeight}px` } : undefined) as CSSProperties | undefined;

  return (
    <section className={['responsive-page-content flex flex-col flex-1 min-h-0 h-full', className ?? ''].filter(Boolean).join(' ')} style={style}>
      <div className="responsive-page-content__inner flex flex-col flex-1 min-h-0 h-full">{children}</div>
    </section>
  );
}
