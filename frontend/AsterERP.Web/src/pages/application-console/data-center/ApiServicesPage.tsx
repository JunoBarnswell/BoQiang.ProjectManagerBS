import { dataCenterModules } from './dataCenterModuleConfig';
import { DataCenterModulePage } from './DataCenterModulePage';

export function ApiServicesPage() {
  return <DataCenterModulePage config={dataCenterModules['api-services']} />;
}
