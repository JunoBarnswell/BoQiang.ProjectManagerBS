import { FormControlInspectorDefinitionBase } from '../base/FormControlInspectorDefinitionBase';

export class InputTextInspectorDefinition extends FormControlInspectorDefinitionBase {
  public constructor() {
    super('input.text');
    this.property({ path: 'props.inputType', section: 'content', order: 30, editor: 'select', valueType: 'string', defaultValue: 'text', labelKey: 'lowCode.inspector.input.type.label', helpKey: 'lowCode.inspector.input.type.help', fallbackLabel: 'Input type', runtimeConsumer: 'runtime.input.type', options: [{ label: 'Text', value: 'text' }, { label: 'Search', value: 'search' }, { label: 'Password', value: 'password' }] });
    this.property({ path: 'props.autoComplete', section: 'content', order: 40, editor: 'text', valueType: 'string', defaultValue: '', labelKey: 'lowCode.inspector.input.autocomplete.label', helpKey: 'lowCode.inspector.input.autocomplete.help', fallbackLabel: 'Autocomplete', runtimeConsumer: 'runtime.input.autocomplete' });
    this.property({ path: 'props.maxLength', section: 'content', order: 50, editor: 'number', valueType: 'number', defaultValue: 0, labelKey: 'lowCode.inspector.input.maxLength.label', helpKey: 'lowCode.inspector.input.maxLength.help', fallbackLabel: 'Maximum length', runtimeConsumer: 'runtime.input.maxLength', validation: { valueType: 'number', integer: true, min: 0 } });
  }
}
