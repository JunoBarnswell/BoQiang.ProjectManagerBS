import { useI18n } from '../../core/i18n/I18nProvider';

import { PageStateShell } from './PageStateShell';

export function Page500() {
  const { translate } = useI18n();
  return <PageStateShell title={translate('status.500.title')} description={translate('status.500.description')} />;
}
