import { TableInspectorDefinitionBase } from '../base/TableInspectorDefinitionBase';

export class TableSemanticInspectorDefinition extends TableInspectorDefinitionBase {
  public constructor() {
    super('table.semantic');
    this.property({ path: 'props.caption', section: 'content', order: 10, editor: 'text', valueType: 'string', defaultValue: '', labelKey: 'lowCode.inspector.table.caption.label', helpKey: 'lowCode.inspector.table.caption.help', fallbackLabel: 'Caption', runtimeConsumer: 'runtime.table.caption' });
    this.property({ path: 'props.loadingState', section: 'content', order: 20, editor: 'text', valueType: 'string', defaultValue: 'Loading', labelKey: 'lowCode.inspector.table.loadingState.label', helpKey: 'lowCode.inspector.table.loadingState.help', fallbackLabel: 'Loading state', runtimeConsumer: 'runtime.table.loadingState' });
    this.property({ path: 'props.errorState', section: 'content', order: 30, editor: 'text', valueType: 'string', defaultValue: 'Unable to load table data', labelKey: 'lowCode.inspector.table.errorState.label', helpKey: 'lowCode.inspector.table.errorState.help', fallbackLabel: 'Error state', runtimeConsumer: 'runtime.table.errorState' });
    this.property({ path: 'props.emptyState', section: 'content', order: 40, editor: 'text', valueType: 'string', defaultValue: 'No data', labelKey: 'lowCode.inspector.table.emptyState.label', helpKey: 'lowCode.inspector.table.emptyState.help', fallbackLabel: 'Empty state', runtimeConsumer: 'runtime.table.emptyState' });
  }
}
