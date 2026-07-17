import { TableInspectorDefinitionBase } from '../base/TableInspectorDefinitionBase';

export class TableCaptionInspectorDefinition extends TableInspectorDefinitionBase {
  public constructor() {
    super('table.caption');
this.property({ path: 'props.captionSide', section: 'content', order: 30, editor: 'select', valueType: 'string', defaultValue: 'top', labelKey: 'lowCode.inspector.table.captionSide.label', helpKey: 'lowCode.inspector.table.captionSide.help', fallbackLabel: 'Caption side', runtimeConsumer: 'runtime.table.captionSide', options: [{ label: 'Top', value: 'top' }, { label: 'Bottom', value: 'bottom' }] });
  }
}
