import { ModalInspectorDefinitionBase } from '../base/ModalInspectorDefinitionBase';

export class InteractionDialogInspectorDefinition extends ModalInspectorDefinitionBase {
  public constructor() {
    super('interaction.dialog');
this.property({ path: 'props.open', section: 'interaction', order: 30, editor: 'boolean', valueType: 'boolean', defaultValue: false, labelKey: 'lowCode.inspector.interaction.open.label', helpKey: 'lowCode.inspector.interaction.open.help', fallbackLabel: 'Open', bindable: true, acceptedSources: ['page', 'component', 'variable'], runtimeConsumer: 'runtime.interaction.open' });
    this.property({ path: 'props.closeOnEscape', section: 'interaction', order: 40, editor: 'boolean', valueType: 'boolean', defaultValue: true, labelKey: 'lowCode.inspector.interaction.closeOnEscape.label', helpKey: 'lowCode.inspector.interaction.closeOnEscape.help', fallbackLabel: 'Close on Escape', runtimeConsumer: 'runtime.interaction.closeOnEscape' });
  }
}
