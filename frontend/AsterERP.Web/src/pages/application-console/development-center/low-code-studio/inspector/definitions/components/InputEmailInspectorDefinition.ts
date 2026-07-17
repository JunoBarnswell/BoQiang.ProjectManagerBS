import { FormControlInspectorDefinitionBase } from '../base/FormControlInspectorDefinitionBase';

export class InputEmailInspectorDefinition extends FormControlInspectorDefinitionBase {
  public constructor() {
    super('input.email');
    this.property({ path: 'props.autocomplete', section: 'content', order: 30, editor: 'text', valueType: 'string', defaultValue: 'email', labelKey: 'lowCode.inspector.input.autocomplete.label', helpKey: 'lowCode.inspector.input.autocomplete.help', fallbackLabel: 'Autocomplete', runtimeConsumer: 'runtime.input.autocomplete' });
    this.property({ path: 'props.pattern', section: 'content', order: 40, editor: 'text', valueType: 'string', defaultValue: '', labelKey: 'lowCode.inspector.input.pattern.label', helpKey: 'lowCode.inspector.input.pattern.help', fallbackLabel: 'Pattern', runtimeConsumer: 'runtime.input.pattern' });
  }
}
