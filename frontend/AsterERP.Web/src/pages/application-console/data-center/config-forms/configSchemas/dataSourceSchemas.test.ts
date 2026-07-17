import { describe, expect, it } from 'vitest';

import {
  applicationDataSourceSslModes,
  validateApplicationDataSourceConfig
} from '../../../../../api/application-data-center/applicationDataSourceSslMode';

import { dataSourceSchemas } from './dataSourceSchemas';

function findField(objectType: string, name: string) {
  const schema = dataSourceSchemas.find((item) => item.objectType === objectType);
  return schema?.sections.flatMap((section) => section.fields).find((field) => field.name === name);
}

describe('data source provider defaults', () => {
  it('uses the backend-safe preferred SSL default for MySQL and PostgreSQL', () => {
    expect(findField('MySql', 'sslMode')?.defaultValue).toBe('Preferred');
    expect(findField('PostgreSQL', 'sslMode')?.defaultValue).toBe('Preferred');
  });

  it('keeps SQL Server transport policy explicit instead of showing a generic SSL mode', () => {
    expect(findField('SqlServer', 'sslMode')).toBeUndefined();
    expect(findField('SqlServer', 'charset')).toBeUndefined();
    expect(findField('SqlServer', 'encrypt')?.defaultValue).toBe(true);
    expect(findField('SqlServer', 'trustServerCertificate')?.defaultValue).toBe(false);
  });

  it('uses provider-specific SSL capability values and connection defaults', () => {
    expect(findField('MySql', 'sslMode')?.options?.map((item) => item.value)).toEqual([...applicationDataSourceSslModes.MySql]);
    expect(findField('PostgreSQL', 'sslMode')?.options?.map((item) => item.value)).toEqual([...applicationDataSourceSslModes.PostgreSQL]);
    expect(findField('PostgreSQL', 'charset')?.defaultValue).toBe('UTF8');
    expect(findField('MySql', 'timeoutSeconds')?.defaultValue).toBe(15);
    expect(findField('MySql', 'poolSize')?.defaultValue).toBe(20);
  });

  it('rejects unsupported or malformed data source connection values before the API call', () => {
    expect(() => validateApplicationDataSourceConfig('PostgreSQL', '{"sslMode":"Prefer"}')).toThrow();
    expect(() => validateApplicationDataSourceConfig('SqlServer', '{"sslMode":"Required"}')).toThrow();
    expect(() => validateApplicationDataSourceConfig('SqlServer', '{"charset":"UTF8"}')).toThrow();
    expect(() => validateApplicationDataSourceConfig('MySql', '{"timeoutSeconds":0}')).toThrow();
    expect(() => validateApplicationDataSourceConfig('PostgreSQL', '{"sslMode":"Preferred","timeoutSeconds":15,"poolSize":20}')).not.toThrow();
  });
});
