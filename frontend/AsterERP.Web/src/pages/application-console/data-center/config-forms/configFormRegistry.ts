import type { ApplicationDataCenterModuleKey } from '../../../../api/application-data-center/applicationDataCenter.types';

import type { ConfigFormSchema } from './configFormTypes';
import { apiServiceSchemas } from './configSchemas/apiServiceSchemas';
import { connectionTestSchemas } from './configSchemas/connectionTestSchemas';
import { dataModelSchemas } from './configSchemas/dataModelSchemas';
import { dataSourceSchemas } from './configSchemas/dataSourceSchemas';
import { dictionaryCodeSchemas } from './configSchemas/dictionaryCodeSchemas';
import { entityFieldSchemas } from './configSchemas/entityFieldSchemas';
import { integrationTaskSchemas } from './configSchemas/integrationTaskSchemas';
import { queryDatasetSchemas } from './configSchemas/queryDatasetSchemas';

const schemas = [
  ...dataSourceSchemas,
  ...connectionTestSchemas,
  ...entityFieldSchemas,
  ...dictionaryCodeSchemas,
  ...queryDatasetSchemas,
  ...integrationTaskSchemas,
  ...dataModelSchemas,
  ...apiServiceSchemas
];

const registry = new Map<string, ConfigFormSchema>();

for (const schema of schemas) {
  registry.set(buildKey(schema.moduleKey, schema.objectType), schema);
}

export function getConfigFormSchema(moduleKey: ApplicationDataCenterModuleKey | string, objectType: string): ConfigFormSchema | null {
  return registry.get(buildKey(moduleKey, objectType)) ?? null;
}

export function getModuleConfigFormSchemas(moduleKey: ApplicationDataCenterModuleKey | string): ConfigFormSchema[] {
  return schemas.filter((schema) => schema.moduleKey === moduleKey);
}

function buildKey(moduleKey: ApplicationDataCenterModuleKey | string, objectType: string): string {
  return `${moduleKey}:${objectType}`.toLowerCase();
}
