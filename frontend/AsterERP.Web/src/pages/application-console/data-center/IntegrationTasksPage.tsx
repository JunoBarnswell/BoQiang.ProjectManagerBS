import { dataCenterModules } from './dataCenterModuleConfig';
import { DataCenterModulePage } from './DataCenterModulePage';

export function IntegrationTasksPage() {
  return <DataCenterModulePage config={dataCenterModules['integration-tasks']} />;
}
