import { TableInspectorDefinitionBase } from '../base/TableInspectorDefinitionBase';

export class TableTdInspectorDefinition extends TableInspectorDefinitionBase {
  public constructor() {
    super('table.td');
this.property({ path: 'props.colSpan', section: 'content', order: 30, editor: 'number', valueType: 'number', defaultValue: 1, labelKey: 'lowCode.inspector.table.colSpan.label', helpKey: 'lowCode.inspector.table.colSpan.help', fallbackLabel: 'Column span', runtimeConsumer: 'runtime.table.colSpan', validation: { valueType: 'number', integer: true, min: 1 } });
    this.property({ path: 'props.rowSpan', section: 'content', order: 40, editor: 'number', valueType: 'number', defaultValue: 1, labelKey: 'lowCode.inspector.table.rowSpan.label', helpKey: 'lowCode.inspector.table.rowSpan.help', fallbackLabel: 'Row span', runtimeConsumer: 'runtime.table.rowSpan', validation: { valueType: 'number', integer: true, min: 1 } });
  }
}
