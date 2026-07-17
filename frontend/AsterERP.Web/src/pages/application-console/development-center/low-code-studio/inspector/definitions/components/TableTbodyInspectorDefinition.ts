import { TableInspectorDefinitionBase } from '../base/TableInspectorDefinitionBase';

export class TableTbodyInspectorDefinition extends TableInspectorDefinitionBase {
  public constructor() {
    super('table.tbody');
    this.onlyInherited();
  }
}
