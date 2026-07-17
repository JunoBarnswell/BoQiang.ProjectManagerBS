import { TableInspectorDefinitionBase } from '../base/TableInspectorDefinitionBase';

export class TableColgroupInspectorDefinition extends TableInspectorDefinitionBase {
  public constructor() {
    super('table.colgroup');
    this.onlyInherited();
  }
}
