import { ContainerInspectorDefinitionBase } from '../base/ContainerInspectorDefinitionBase';

export class ListLiInspectorDefinition extends ContainerInspectorDefinitionBase {
  public constructor() {
    super('list.li');
this.property({ path: 'props.value', section: 'content', order: 30, editor: 'number', valueType: 'number', defaultValue: 0, labelKey: 'lowCode.inspector.list.value.label', helpKey: 'lowCode.inspector.list.value.help', fallbackLabel: 'List value', runtimeConsumer: 'runtime.list.value', validation: { valueType: 'number', integer: true } });
  }
}
