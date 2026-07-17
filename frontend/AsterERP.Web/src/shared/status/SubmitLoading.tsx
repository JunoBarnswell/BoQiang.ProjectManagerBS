import { useI18n } from '../../core/i18n/I18nProvider';

export function SubmitLoading() {
  const { translate } = useI18n();
  return <span className="submit-loading">{translate('workflow.drawer.submitting')}</span>;
}
