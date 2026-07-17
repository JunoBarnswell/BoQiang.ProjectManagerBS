import { TableInspectorDefinitionBase } from '../base/TableInspectorDefinitionBase';

export class TableTfootInspectorDefinition extends TableInspectorDefinitionBase {
  public constructor() {
    super('table.tfoot');
    this.onlyInherited();
  }
}
