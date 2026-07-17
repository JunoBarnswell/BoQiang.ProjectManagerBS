import { FormControlInspectorDefinitionBase } from './FormControlInspectorDefinitionBase';

export abstract class ChoiceInspectorDefinitionBase extends FormControlInspectorDefinitionBase {
  protected constructor(componentType: string) {
    super(componentType);
    this.property({ path: 'props.options', section: 'data', order: 20, editor: 'json', valueType: 'array', defaultValue: [], labelKey: 'lowCode.inspector.choice.options.label', helpKey: 'lowCode.inspector.choice.options.help', fallbackLabel: 'Options', bindable: true, acceptedSources: ['page', 'component', 'variable', 'dataset'], runtimeConsumer: 'runtime.choice.options' });
  }
}
