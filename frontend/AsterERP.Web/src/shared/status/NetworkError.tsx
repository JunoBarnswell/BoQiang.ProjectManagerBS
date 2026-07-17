import type { ReactNode } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';

import { PageStateShell } from './PageStateShell';

interface NetworkErrorProps {
  action?: ReactNode;
  description?: ReactNode;
}

export function NetworkError({ action, description }: NetworkErrorProps) {
  const { translate } = useI18n();
  return (
    <PageStateShell
      action={action}
      description={description ?? translate('status.network.description')}
      title={translate('status.network.title')}
    />
  );
}
