import { TableInspectorDefinitionBase } from '../base/TableInspectorDefinitionBase';

export class TableThInspectorDefinition extends TableInspectorDefinitionBase {
  public constructor() {
    super('table.th');
this.property({ path: 'props.scope', section: 'content', order: 30, editor: 'select', valueType: 'string', defaultValue: 'col', labelKey: 'lowCode.inspector.table.scope.label', helpKey: 'lowCode.inspector.table.scope.help', fallbackLabel: 'Scope', runtimeConsumer: 'runtime.table.scope', options: [{ label: 'Column', value: 'col' }, { label: 'Row', value: 'row' }, { label: 'Column group', value: 'colgroup' }, { label: 'Row group', value: 'rowgroup' }] });
    this.property({ path: 'props.headers', section: 'content', order: 40, editor: 'text', valueType: 'string', defaultValue: '', labelKey: 'lowCode.inspector.table.headers.label', helpKey: 'lowCode.inspector.table.headers.help', fallbackLabel: 'Headers', runtimeConsumer: 'runtime.table.headers' });
  }
}
