import { InteractiveInspectorDefinitionBase } from './InteractiveInspectorDefinitionBase';

export abstract class ModalInspectorDefinitionBase extends InteractiveInspectorDefinitionBase {
  protected constructor(componentType: string) {
    super(componentType);
    this.property({ path: 'props.title', section: 'content', order: 10, editor: 'text', valueType: 'string', defaultValue: 'Dialog', labelKey: 'lowCode.inspector.modal.title.label', helpKey: 'lowCode.inspector.modal.title.help', fallbackLabel: 'Title', bindable: true, acceptedSources: ['page', 'component', 'variable'], runtimeConsumer: 'runtime.modal.title' });
  }
}
