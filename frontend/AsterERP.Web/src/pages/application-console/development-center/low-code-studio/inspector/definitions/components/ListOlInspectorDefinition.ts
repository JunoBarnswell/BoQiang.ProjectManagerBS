import { ContainerInspectorDefinitionBase } from '../base/ContainerInspectorDefinitionBase';

export class ListOlInspectorDefinition extends ContainerInspectorDefinitionBase {
  public constructor() {
    super('list.ol');
this.property({ path: 'props.start', section: 'content', order: 30, editor: 'number', valueType: 'number', defaultValue: 1, labelKey: 'lowCode.inspector.list.start.label', helpKey: 'lowCode.inspector.list.start.help', fallbackLabel: 'Start', runtimeConsumer: 'runtime.list.start', validation: { valueType: 'number', integer: true } });
    this.property({ path: 'props.reversed', section: 'content', order: 40, editor: 'boolean', valueType: 'boolean', defaultValue: false, labelKey: 'lowCode.inspector.list.reversed.label', helpKey: 'lowCode.inspector.list.reversed.help', fallbackLabel: 'Reversed', runtimeConsumer: 'runtime.list.reversed' });
  }
}
