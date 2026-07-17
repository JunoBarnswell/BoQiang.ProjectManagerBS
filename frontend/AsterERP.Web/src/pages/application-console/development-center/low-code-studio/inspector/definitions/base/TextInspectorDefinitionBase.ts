import { VisualComponentInspectorDefinitionBase } from './VisualComponentInspectorDefinitionBase';

export abstract class TextInspectorDefinitionBase extends VisualComponentInspectorDefinitionBase {
  protected constructor(componentType: string) {
    super(componentType);
    this.property({ path: 'props.text', section: 'content', order: 10, editor: 'text', valueType: 'string', defaultValue: 'Text', labelKey: 'lowCode.inspector.text.text.label', helpKey: 'lowCode.inspector.text.text.help', fallbackLabel: '文本', bindable: true, acceptedSources: ['page', 'component', 'variable'], runtimeConsumer: 'runtime.component.props.text' });
  }
}
