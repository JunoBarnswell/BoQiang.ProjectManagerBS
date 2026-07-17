import type { ReactNode } from 'react';

import { ResponsivePage } from '../../responsive/ResponsivePage';

interface CrudPageProps {
  actions?: ReactNode;
  children: ReactNode;
  className?: string;
  description?: ReactNode;
  editor?: ReactNode;
  eyebrow?: ReactNode;
  fitScreen?: boolean;
  footer?: ReactNode;
  searchArea?: ReactNode;
  title: ReactNode;
  toolbar?: ReactNode;
}

export function CrudPage({
  actions,
  children,
  className,
  description,
  editor,
  eyebrow,
  fitScreen = true,
  footer,
  searchArea,
  title,
  toolbar
}: CrudPageProps) {
  return (
    <ResponsivePage
      actions={actions}
      className={className}
      description={description}
      eyebrow={eyebrow}
      footer={footer}
      fitScreen={fitScreen}
      searchArea={searchArea}
      title={title}
      toolbar={toolbar}
    >
      {children}
      {editor}
    </ResponsivePage>
  );
}
