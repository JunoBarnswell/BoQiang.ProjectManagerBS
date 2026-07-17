import { TableInspectorDefinitionBase } from '../base/TableInspectorDefinitionBase';

export class TableTheadInspectorDefinition extends TableInspectorDefinitionBase {
  public constructor() {
    super('table.thead');
    this.onlyInherited();
  }
}
