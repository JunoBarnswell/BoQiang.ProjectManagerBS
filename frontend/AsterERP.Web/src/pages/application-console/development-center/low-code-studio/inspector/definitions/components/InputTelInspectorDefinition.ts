import { FormControlInspectorDefinitionBase } from '../base/FormControlInspectorDefinitionBase';

export class InputTelInspectorDefinition extends FormControlInspectorDefinitionBase {
  public constructor() {
    super('input.tel');
    this.property({ path: 'props.autoComplete', section: 'content', order: 30, editor: 'text', valueType: 'string', defaultValue: 'tel', labelKey: 'lowCode.inspector.input.autocomplete.label', helpKey: 'lowCode.inspector.input.autocomplete.help', fallbackLabel: 'Autocomplete', runtimeConsumer: 'runtime.input.autocomplete' });
    this.property({ path: 'props.maxLength', section: 'content', order: 40, editor: 'number', valueType: 'number', defaultValue: 0, labelKey: 'lowCode.inspector.input.maxLength.label', helpKey: 'lowCode.inspector.input.maxLength.help', fallbackLabel: 'Maximum length', runtimeConsumer: 'runtime.input.maxLength', validation: { valueType: 'number', integer: true, min: 0 } });
  }
}
