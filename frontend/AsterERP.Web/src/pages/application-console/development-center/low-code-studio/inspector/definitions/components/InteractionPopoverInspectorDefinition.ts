import { ModalInspectorDefinitionBase } from '../base/ModalInspectorDefinitionBase';

export class InteractionPopoverInspectorDefinition extends ModalInspectorDefinitionBase {
  public constructor() {
    super('interaction.popover');
this.property({ path: 'props.open', section: 'interaction', order: 30, editor: 'boolean', valueType: 'boolean', defaultValue: false, labelKey: 'lowCode.inspector.interaction.open.label', helpKey: 'lowCode.inspector.interaction.open.help', fallbackLabel: 'Open', bindable: true, acceptedSources: ['page', 'component', 'variable'], runtimeConsumer: 'runtime.interaction.open' });
    this.property({ path: 'props.placement', section: 'interaction', order: 40, editor: 'select', valueType: 'string', defaultValue: 'bottom', labelKey: 'lowCode.inspector.interaction.placement.label', helpKey: 'lowCode.inspector.interaction.placement.help', fallbackLabel: 'Placement', runtimeConsumer: 'runtime.interaction.placement', options: [{ label: 'Top', value: 'top' }, { label: 'Bottom', value: 'bottom' }, { label: 'Left', value: 'left' }, { label: 'Right', value: 'right' }] });
  }
}
