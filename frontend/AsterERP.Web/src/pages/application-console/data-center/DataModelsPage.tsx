import { dataCenterModules } from './dataCenterModuleConfig';
import { DataCenterModulePage } from './DataCenterModulePage';

export function DataModelsPage() {
  return <DataCenterModulePage config={dataCenterModules.models} />;
}
