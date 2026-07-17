import { dataCenterModules } from './dataCenterModuleConfig';
import { DataCenterModulePage } from './DataCenterModulePage';

export function EntitiesFieldsPage() {
  return <DataCenterModulePage config={dataCenterModules['entities-fields']} />;
}
