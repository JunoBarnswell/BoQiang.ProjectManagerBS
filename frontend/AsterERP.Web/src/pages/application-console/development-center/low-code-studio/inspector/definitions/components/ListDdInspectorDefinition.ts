import { ContainerInspectorDefinitionBase } from '../base/ContainerInspectorDefinitionBase';

export class ListDdInspectorDefinition extends ContainerInspectorDefinitionBase {
  public constructor() {
    super('list.dd');
this.property({ path: 'props.description', section: 'content', order: 30, editor: 'text', valueType: 'string', defaultValue: '', labelKey: 'lowCode.inspector.list.description.label', helpKey: 'lowCode.inspector.list.description.help', fallbackLabel: 'Description', runtimeConsumer: 'runtime.list.description' });
  }
}
