import { flowiseI18nKeys } from './flowiseI18nKeys';

export function translateFlowiseStatus(status: string, translate: (key: string) => string): string {
  switch (status) {
    case 'Completed':
      return translate(flowiseI18nKeys.status.completed);
    case 'Authorized':
      return translate(flowiseI18nKeys.status.authorized);
    case 'Disabled':
      return translate(flowiseI18nKeys.status.disabled);
    case 'Draft':
      return translate(flowiseI18nKeys.status.draft);
    case 'Enabled':
      return translate(flowiseI18nKeys.status.enabled);
    case 'Error':
      return translate(flowiseI18nKeys.status.error);
    case 'Failed':
      return translate(flowiseI18nKeys.status.failed);
    case 'Running':
      return translate(flowiseI18nKeys.status.running);
    default:
      return status;
  }
}
