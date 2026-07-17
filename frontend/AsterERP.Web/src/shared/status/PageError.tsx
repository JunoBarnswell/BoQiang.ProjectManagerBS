import type { ReactNode } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';

import { PageStateShell } from './PageStateShell';

interface PageErrorProps {
  action?: ReactNode;
  description?: ReactNode;
}

export function PageError({ action, description }: PageErrorProps) {
  const { translate } = useI18n();
  return (
    <PageStateShell
      action={action}
      description={description ?? translate('status.error.description')}
      title={translate('status.error.title')}
    />
  );
}
