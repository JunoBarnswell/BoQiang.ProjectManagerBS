import { useI18n } from '../../core/i18n/I18nProvider';

import { PageStateShell } from './PageStateShell';

export function PageLoading() {
  const { translate } = useI18n();
  return <PageStateShell title={translate('status.loading.title')} description={translate('status.loading.description')} />;
}
