import { FormControlInspectorDefinitionBase } from '../base/FormControlInspectorDefinitionBase';

export class InputNumberInspectorDefinition extends FormControlInspectorDefinitionBase {
  public constructor() {
    super('input.number');
    this.property({ path: 'props.min', section: 'content', order: 30, editor: 'number', valueType: 'number', defaultValue: 0, labelKey: 'lowCode.inspector.input.min.label', helpKey: 'lowCode.inspector.input.min.help', fallbackLabel: 'Minimum', runtimeConsumer: 'runtime.input.min' });
    this.property({ path: 'props.max', section: 'content', order: 40, editor: 'number', valueType: 'number', defaultValue: 100, labelKey: 'lowCode.inspector.input.max.label', helpKey: 'lowCode.inspector.input.max.help', fallbackLabel: 'Maximum', runtimeConsumer: 'runtime.input.max' });
    this.property({ path: 'props.step', section: 'content', order: 50, editor: 'number', valueType: 'number', defaultValue: 1, labelKey: 'lowCode.inspector.input.step.label', helpKey: 'lowCode.inspector.input.step.help', fallbackLabel: 'Step', runtimeConsumer: 'runtime.input.step', validation: { valueType: 'number', min: 0 } });
  }
}
