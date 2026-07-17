import { TableInspectorDefinitionBase } from '../base/TableInspectorDefinitionBase';

export class TableColInspectorDefinition extends TableInspectorDefinitionBase {
  public constructor() {
    super('table.col');
    this.onlyInherited();
  }
}
