import { ContainerInspectorDefinitionBase } from '../base/ContainerInspectorDefinitionBase';

export class ListDtInspectorDefinition extends ContainerInspectorDefinitionBase {
  public constructor() {
    super('list.dt');
this.property({ path: 'props.term', section: 'content', order: 30, editor: 'text', valueType: 'string', defaultValue: '', labelKey: 'lowCode.inspector.list.term.label', helpKey: 'lowCode.inspector.list.term.help', fallbackLabel: 'Term', runtimeConsumer: 'runtime.list.term' });
  }
}
