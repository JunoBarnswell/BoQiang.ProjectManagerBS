import { dataCenterModules } from './dataCenterModuleConfig';
import { DataCenterModulePage } from './DataCenterModulePage';

export function DataSourcesPage() {
  return <DataCenterModulePage config={dataCenterModules['data-sources']} />;
}
