import { FormControlInspectorDefinitionBase } from '../base/FormControlInspectorDefinitionBase';

export class InputDateInspectorDefinition extends FormControlInspectorDefinitionBase {
  public constructor() {
    super('input.date');
    this.property({ path: 'props.autoComplete', section: 'content', order: 30, editor: 'text', valueType: 'string', defaultValue: 'off', labelKey: 'lowCode.inspector.input.autocomplete.label', helpKey: 'lowCode.inspector.input.autocomplete.help', fallbackLabel: 'Autocomplete', runtimeConsumer: 'runtime.input.autocomplete' });
  }
}
