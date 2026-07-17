export type MessageParams = Record<string, string | number | boolean | null | undefined>;

const templatePattern = /\{([A-Za-z0-9_.:-]+)\}/g;

export function formatMessage(template: string, params?: MessageParams): string {
  if (!params) {
    return template;
  }

  return template.replace(templatePattern, (_, key: string) => {
    const value = params[key];
    return value === null || value === undefined ? '' : String(value);
  });
}
