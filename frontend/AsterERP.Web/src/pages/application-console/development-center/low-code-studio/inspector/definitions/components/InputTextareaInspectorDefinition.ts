import { FormControlInspectorDefinitionBase } from '../base/FormControlInspectorDefinitionBase';

export class InputTextareaInspectorDefinition extends FormControlInspectorDefinitionBase {
  public constructor() {
    super('input.textarea');
    this.property({ path: 'props.rows', section: 'content', order: 30, editor: 'number', valueType: 'number', defaultValue: 4, labelKey: 'lowCode.inspector.input.rows.label', helpKey: 'lowCode.inspector.input.rows.help', fallbackLabel: 'Rows', runtimeConsumer: 'runtime.input.rows', validation: { valueType: 'number', integer: true, min: 1 } });
    this.property({ path: 'props.resize', section: 'content', order: 40, editor: 'select', valueType: 'string', defaultValue: 'vertical', labelKey: 'lowCode.inspector.input.resize.label', helpKey: 'lowCode.inspector.input.resize.help', fallbackLabel: 'Resize', runtimeConsumer: 'runtime.input.resize', options: [{ label: 'None', value: 'none' }, { label: 'Vertical', value: 'vertical' }, { label: 'Both', value: 'both' }] });
  }
}
