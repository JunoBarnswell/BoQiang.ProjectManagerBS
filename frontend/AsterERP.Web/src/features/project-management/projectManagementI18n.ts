import { formatMessage } from '../../core/i18n/formatMessage';
import { useI18n, type I18nContextValue } from '../../core/i18n/I18nProvider';

export function useProjectManagementI18n() {
  const { locale, translate } = useI18n();
  return {
    locale,
    t: translate,
    format: (key: string, values?: Record<string, string | number>) => formatMessage(translate(key), values),
    date: (value: string | Date, options?: Intl.DateTimeFormatOptions) => new Intl.DateTimeFormat(locale, options).format(new Date(value)),
    dateTime: (value: string | Date) => new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
  };
}

export function projectManagementEnumLabel(translate: I18nContextValue['translate'], group: 'priority' | 'status' | 'workItemType' | 'risk' | 'requirementType' | 'requirementSource', value: string): string {
  const key = `projectManagement.enum.${group}.${value}`;
  const translated = translate(key);
  return translated === key ? value : translated;
}
