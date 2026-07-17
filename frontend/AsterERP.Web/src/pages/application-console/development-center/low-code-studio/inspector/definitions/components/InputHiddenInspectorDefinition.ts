import { ComponentInspectorDefinitionBase } from '../base/ComponentInspectorDefinitionBase';

export class InputHiddenInspectorDefinition extends ComponentInspectorDefinitionBase {
  public constructor() {
    super('input.hidden');
    this.property({ path: 'props.value', section: 'content', order: 10, editor: 'text', valueType: 'string', defaultValue: '', labelKey: 'lowCode.inspector.form.value.label', helpKey: 'lowCode.inspector.form.value.help', fallbackLabel: 'Value', bindable: true, acceptedSources: ['page', 'component', 'variable', 'dataset'], runtimeConsumer: 'runtime.form.value' });
  }
}
