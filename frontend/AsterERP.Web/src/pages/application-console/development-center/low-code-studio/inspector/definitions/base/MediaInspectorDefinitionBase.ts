import { VisualComponentInspectorDefinitionBase } from './VisualComponentInspectorDefinitionBase';

export abstract class MediaInspectorDefinitionBase extends VisualComponentInspectorDefinitionBase {
  protected constructor(componentType: string) {
    super(componentType);
    this.property({ path: 'props.src', section: 'content', order: 10, editor: 'text', valueType: 'string', defaultValue: '', labelKey: 'lowCode.inspector.media.source.label', helpKey: 'lowCode.inspector.media.source.help', fallbackLabel: 'Source', bindable: true, acceptedSources: ['page', 'component', 'variable', 'dataset'], runtimeConsumer: 'runtime.media.source' });
    this.property({ path: 'props.alt', section: 'content', order: 20, editor: 'text', valueType: 'string', defaultValue: '', labelKey: 'lowCode.inspector.media.alt.label', helpKey: 'lowCode.inspector.media.alt.help', fallbackLabel: 'Alternative text', bindable: true, acceptedSources: ['page', 'component', 'variable'], runtimeConsumer: 'runtime.media.alt' });
  }
}
