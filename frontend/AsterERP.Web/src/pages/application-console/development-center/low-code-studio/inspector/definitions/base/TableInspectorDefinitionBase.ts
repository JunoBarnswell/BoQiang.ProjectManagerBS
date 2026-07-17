import { DataComponentInspectorDefinitionBase } from './DataComponentInspectorDefinitionBase';

export abstract class TableInspectorDefinitionBase extends DataComponentInspectorDefinitionBase {
  protected constructor(componentType: string) {
    super(componentType);
    this.property({ path: 'props.columns', section: 'data', order: 30, editor: 'json', valueType: 'array', defaultValue: [], labelKey: 'lowCode.inspector.table.columns.label', helpKey: 'lowCode.inspector.table.columns.help', fallbackLabel: 'Columns', bindable: true, acceptedSources: ['page', 'component', 'variable', 'dataset'], runtimeConsumer: 'runtime.table.columns' });
    this.property({ path: 'props.rows', section: 'data', order: 40, editor: 'json', valueType: 'array', defaultValue: [], labelKey: 'lowCode.inspector.table.rows.label', helpKey: 'lowCode.inspector.table.rows.help', fallbackLabel: 'Rows', bindable: true, acceptedSources: ['page', 'component', 'variable', 'dataset'], runtimeConsumer: 'runtime.table.rows' });
  }
}
