import { InteractiveInspectorDefinitionBase } from './InteractiveInspectorDefinitionBase';

export abstract class FormControlInspectorDefinitionBase extends InteractiveInspectorDefinitionBase {
  protected constructor(componentType: string) {
    super(componentType);
    this.property({ path: 'bindings.data.field', section: 'data', order: 10, editor: 'text', valueType: 'string', defaultValue: '', labelKey: 'lowCode.inspector.common.dataField.label', helpKey: 'lowCode.inspector.common.dataField.help', fallbackLabel: 'Data field', bindable: true, acceptedSources: ['page', 'component', 'variable', 'dataset'], responsive: { enabled: false, mode: 'inherit' }, runtimeConsumer: 'runtime.binding.data' });
    this.property({ path: 'props.value', section: 'content', order: 10, editor: 'text', valueType: 'string', defaultValue: '', labelKey: 'lowCode.inspector.form.value.label', helpKey: 'lowCode.inspector.form.value.help', fallbackLabel: 'Value', bindable: true, acceptedSources: ['page', 'component', 'variable', 'dataset'], runtimeConsumer: 'runtime.form.value' });
    this.property({ path: 'props.placeholder', section: 'content', order: 20, editor: 'text', valueType: 'string', defaultValue: '', labelKey: 'lowCode.inspector.form.placeholder.label', helpKey: 'lowCode.inspector.form.placeholder.help', fallbackLabel: 'Placeholder', bindable: true, acceptedSources: ['page', 'component', 'variable'], runtimeConsumer: 'runtime.form.placeholder' });
  }
}
