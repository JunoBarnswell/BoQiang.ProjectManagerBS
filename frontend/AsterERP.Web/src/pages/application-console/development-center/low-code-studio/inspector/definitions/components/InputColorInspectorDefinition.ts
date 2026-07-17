import { FormControlInspectorDefinitionBase } from '../base/FormControlInspectorDefinitionBase';

export class InputColorInspectorDefinition extends FormControlInspectorDefinitionBase {
  public constructor() {
    super('input.color');
    this.property({ path: 'props.inputType', section: 'content', order: 30, editor: 'select', valueType: 'string', defaultValue: 'color', labelKey: 'lowCode.inspector.input.type.label', helpKey: 'lowCode.inspector.input.type.help', fallbackLabel: 'Input type', runtimeConsumer: 'runtime.input.type', options: [{ label: 'Color', value: 'color' }] });
  }
}
