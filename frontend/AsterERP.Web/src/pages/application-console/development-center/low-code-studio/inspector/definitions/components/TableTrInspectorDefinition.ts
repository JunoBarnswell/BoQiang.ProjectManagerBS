import { TableInspectorDefinitionBase } from '../base/TableInspectorDefinitionBase';

export class TableTrInspectorDefinition extends TableInspectorDefinitionBase {
  public constructor() {
    super('table.tr');
    this.onlyInherited();
  }
}
