import { VisualComponentInspectorDefinitionBase } from './VisualComponentInspectorDefinitionBase';

export abstract class InteractiveInspectorDefinitionBase extends VisualComponentInspectorDefinitionBase {
  protected constructor(componentType: string) {
    super(componentType);
    this.property({ path: 'props.loading', section: 'interaction', order: 10, editor: 'boolean', valueType: 'boolean', defaultValue: false, labelKey: 'lowCode.inspector.common.loading.label', helpKey: 'lowCode.inspector.common.loading.help', fallbackLabel: 'Loading', bindable: true, acceptedSources: ['page', 'component', 'variable'], responsive: { enabled: false, mode: 'inherit' }, runtimeConsumer: 'runtime.component.loading' });
    this.property({ path: 'props.disabled', section: 'interaction', order: 20, editor: 'boolean', valueType: 'boolean', defaultValue: false, labelKey: 'lowCode.inspector.interaction.disabled.label', helpKey: 'lowCode.inspector.interaction.disabled.help', fallbackLabel: 'Disabled', bindable: true, acceptedSources: ['page', 'component', 'variable'], runtimeConsumer: 'runtime.component.props.disabled' });
    this.property({ path: 'props.readOnly', section: 'interaction', order: 30, editor: 'boolean', valueType: 'boolean', defaultValue: false, labelKey: 'lowCode.inspector.interaction.readOnly.label', helpKey: 'lowCode.inspector.interaction.readOnly.help', fallbackLabel: 'Read only', bindable: true, acceptedSources: ['page', 'component', 'variable'], runtimeConsumer: 'runtime.component.props.readOnly' });
  }
}
