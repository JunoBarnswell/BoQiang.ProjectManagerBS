import { FormControlInspectorDefinitionBase } from '../base/FormControlInspectorDefinitionBase';

export class InputFileInspectorDefinition extends FormControlInspectorDefinitionBase {
  public constructor() {
    super('input.file');
    this.property({ path: 'props.accept', section: 'content', order: 30, editor: 'text', valueType: 'string', defaultValue: '', labelKey: 'lowCode.inspector.input.accept.label', helpKey: 'lowCode.inspector.input.accept.help', fallbackLabel: 'Accepted file types', runtimeConsumer: 'runtime.input.accept' });
    this.property({ path: 'props.multiple', section: 'content', order: 40, editor: 'boolean', valueType: 'boolean', defaultValue: false, labelKey: 'lowCode.inspector.input.multiple.label', helpKey: 'lowCode.inspector.input.multiple.help', fallbackLabel: 'Multiple files', runtimeConsumer: 'runtime.input.multiple' });
  }
}
