import { DataComponentInspectorDefinitionBase } from '../base/DataComponentInspectorDefinitionBase';

export class OutputValueInspectorDefinition extends DataComponentInspectorDefinitionBase {
  public constructor() {
    super('output.value');
this.property({ path: 'props.fallback', section: 'content', order: 30, editor: 'text', valueType: 'string', defaultValue: '', labelKey: 'lowCode.inspector.output.fallback.label', helpKey: 'lowCode.inspector.output.fallback.help', fallbackLabel: 'Fallback', runtimeConsumer: 'runtime.output.fallback' });
  }
}
