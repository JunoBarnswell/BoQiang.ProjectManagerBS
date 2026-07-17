import type { ReactNode } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';

import { PageStateShell } from './PageStateShell';

interface PageEmptyProps {
  description?: ReactNode;
}

export function PageEmpty({ description }: PageEmptyProps) {
  const { translate } = useI18n();
  return <PageStateShell title={translate('status.empty.title')} description={description ?? translate('status.empty.description')} />;
}
