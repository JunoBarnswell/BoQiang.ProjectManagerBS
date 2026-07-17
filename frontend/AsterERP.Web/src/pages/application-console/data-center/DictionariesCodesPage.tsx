import { dataCenterModules } from './dataCenterModuleConfig';
import { DataCenterModulePage } from './DataCenterModulePage';

export function DictionariesCodesPage() {
  return <DataCenterModulePage config={dataCenterModules['dictionaries-codes']} />;
}
