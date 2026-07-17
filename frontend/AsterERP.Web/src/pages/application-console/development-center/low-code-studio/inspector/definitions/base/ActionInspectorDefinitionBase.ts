import { InteractiveInspectorDefinitionBase } from './InteractiveInspectorDefinitionBase';

export abstract class ActionInspectorDefinitionBase extends InteractiveInspectorDefinitionBase {
  protected constructor(componentType: string) {
    super(componentType);
    this.property({ path: 'props.text', section: 'content', order: 10, editor: 'text', valueType: 'string', defaultValue: 'Button', labelKey: 'lowCode.inspector.action.text.label', helpKey: 'lowCode.inspector.action.text.help', fallbackLabel: 'Button text', bindable: true, acceptedSources: ['page', 'component', 'variable'], runtimeConsumer: 'runtime.action.label' });
    this.property({ path: 'props.variant', section: 'appearance', order: 70, editor: 'select', valueType: 'string', defaultValue: 'primary', labelKey: 'lowCode.inspector.action.variant.label', helpKey: 'lowCode.inspector.action.variant.help', fallbackLabel: 'Variant', runtimeConsumer: 'runtime.action.variant', options: [{ label: 'Primary', value: 'primary' }, { label: 'Secondary', value: 'secondary' }, { label: 'Danger', value: 'danger' }] });
  }
}
