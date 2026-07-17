export const applicationDataSourceSslModes = {
  MySql: ['Disabled', 'Preferred', 'Required', 'VerifyCA', 'VerifyFull'],
  PostgreSQL: ['Disabled', 'Allow', 'Preferred', 'Required', 'VerifyCA', 'VerifyFull']
} as const;

export type ApplicationDataSourceSslMode = (typeof applicationDataSourceSslModes)[keyof typeof applicationDataSourceSslModes][number];

export const applicationDataSourceDefaultSslMode: ApplicationDataSourceSslMode = 'Preferred';

export function getApplicationDataSourceSslModes(objectType: string): readonly ApplicationDataSourceSslMode[] {
  if (objectType === 'MySql') {
    return applicationDataSourceSslModes.MySql;
  }

  if (objectType === 'PostgreSQL') {
    return applicationDataSourceSslModes.PostgreSQL;
  }

  return [];
}

export function isApplicationDataSourceSslMode(value: unknown): value is ApplicationDataSourceSslMode {
  return getApplicationDataSourceSslModes('MySql').includes(value as ApplicationDataSourceSslMode) ||
    getApplicationDataSourceSslModes('PostgreSQL').includes(value as ApplicationDataSourceSslMode);
}

export function validateApplicationDataSourceConfig(objectType: string, configJson: string): void {
  let config: Record<string, unknown>;
  try {
    config = JSON.parse(configJson) as Record<string, unknown>;
  } catch {
    throw new Error('Data source config JSON is invalid.');
  }

  if (Object.prototype.hasOwnProperty.call(config, 'sslMode')) {
    const sslMode = config.sslMode;
    if (typeof sslMode !== 'string' || !getApplicationDataSourceSslModes(objectType).includes(sslMode as ApplicationDataSourceSslMode)) {
      throw new Error(`Unsupported SSL mode for ${objectType}.`);
    }
  }

  if (Object.prototype.hasOwnProperty.call(config, 'charset') && !['MySql', 'PostgreSQL'].includes(objectType)) {
    throw new Error(`Charset is not supported by ${objectType}.`);
  }

  const limits = { poolSize: 1000, timeoutSeconds: 300 } as const;
  for (const field of Object.keys(limits) as Array<keyof typeof limits>) {
    if (Object.prototype.hasOwnProperty.call(config, field) &&
      (!Number.isInteger(config[field]) || Number(config[field]) < 1 || Number(config[field]) > limits[field])) {
      throw new Error(`${field} must be a positive integer.`);
    }
  }
}
