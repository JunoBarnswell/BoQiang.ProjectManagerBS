import { VisualComponentInspectorDefinitionBase } from './VisualComponentInspectorDefinitionBase';

export abstract class ContainerInspectorDefinitionBase extends VisualComponentInspectorDefinitionBase {
  protected constructor(componentType: string) {
    super(componentType);
    this.property({ path: 'props.content', section: 'content', order: 10, editor: 'text', valueType: 'string', defaultValue: '', labelKey: 'lowCode.inspector.container.content.label', helpKey: 'lowCode.inspector.container.content.help', fallbackLabel: 'Content', bindable: true, acceptedSources: ['page', 'component', 'variable'], runtimeConsumer: 'runtime.component.children' });
  }
}
