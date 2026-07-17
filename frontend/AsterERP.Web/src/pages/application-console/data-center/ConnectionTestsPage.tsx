import { dataCenterModules } from './dataCenterModuleConfig';
import { DataCenterModulePage } from './DataCenterModulePage';

export function ConnectionTestsPage() {
  return <DataCenterModulePage config={dataCenterModules['connection-tests']} />;
}
