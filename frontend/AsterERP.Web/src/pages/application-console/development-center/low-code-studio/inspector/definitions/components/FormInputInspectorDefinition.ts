import { FormControlInspectorDefinitionBase } from '../base/FormControlInspectorDefinitionBase';

export class FormInputInspectorDefinition extends FormControlInspectorDefinitionBase {
  public constructor() {
    super('form.input');
    this.property({ path: 'props.inputType', section: 'content', order: 30, editor: 'select', valueType: 'string', defaultValue: 'text', labelKey: 'lowCode.inspector.input.type.label', helpKey: 'lowCode.inspector.input.type.help', fallbackLabel: 'Input type', runtimeConsumer: 'runtime.input.type', options: [{ label: 'Text', value: 'text' }, { label: 'Number', value: 'number' }, { label: 'Date', value: 'date' }, { label: 'File', value: 'file' }] });
  }
}
