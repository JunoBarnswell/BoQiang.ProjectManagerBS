import type { ReactNode } from 'react';

import { translateCurrentLiteral } from '../../core/i18n/I18nProvider';

interface ResponsivePageHeaderProps {
  actions?: ReactNode;
  description?: ReactNode;
  eyebrow?: ReactNode;
  title: ReactNode;
}

export function ResponsivePageHeader({ actions, description, eyebrow, title }: ResponsivePageHeaderProps) {
  const resolvedEyebrow = typeof eyebrow === 'string' ? translateCurrentLiteral(eyebrow) : eyebrow;
  const resolvedTitle = typeof title === 'string' ? translateCurrentLiteral(title) : title;
  const resolvedDescription = typeof description === 'string' ? translateCurrentLiteral(description) : description;

  return (
    <header className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 shrink-0">
      <div className="flex flex-row items-center gap-2 min-w-0">
        {resolvedEyebrow ? (
          <div className="text-xs font-bold tracking-wider text-primary-600 uppercase border-r border-gray-300 pr-2 mr-1 leading-none h-4 flex items-center">
            {resolvedEyebrow}
          </div>
        ) : null}
        <h1 className="text-lg font-bold text-gray-800 m-0 leading-tight truncate">
          {resolvedTitle}
        </h1>
        {resolvedDescription ? (
          <div className="text-xs text-gray-500 ml-2 hidden lg:block truncate max-w-xl">
            {resolvedDescription}
          </div>
        ) : null}
      </div>
      {actions ? (
        <div className="flex flex-wrap items-center gap-2 sm:mt-0 mt-2 shrink-0">
          {actions}
        </div>
      ) : null}
    </header>
  );
}
