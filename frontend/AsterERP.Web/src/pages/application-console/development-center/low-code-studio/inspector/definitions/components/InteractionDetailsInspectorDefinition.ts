import { ContainerInspectorDefinitionBase } from '../base/ContainerInspectorDefinitionBase';

export class InteractionDetailsInspectorDefinition extends ContainerInspectorDefinitionBase {
  public constructor() {
    super('interaction.details');
this.property({ path: 'props.open', section: 'interaction', order: 10, editor: 'boolean', valueType: 'boolean', defaultValue: false, labelKey: 'lowCode.inspector.interaction.open.label', helpKey: 'lowCode.inspector.interaction.open.help', fallbackLabel: 'Open', bindable: true, acceptedSources: ['page', 'component', 'variable'], runtimeConsumer: 'runtime.interaction.open' });
    this.property({ path: 'props.summary', section: 'content', order: 30, editor: 'text', valueType: 'string', defaultValue: 'Details', labelKey: 'lowCode.inspector.interaction.summary.label', helpKey: 'lowCode.inspector.interaction.summary.help', fallbackLabel: 'Summary', runtimeConsumer: 'runtime.interaction.summary' });
  }
}
