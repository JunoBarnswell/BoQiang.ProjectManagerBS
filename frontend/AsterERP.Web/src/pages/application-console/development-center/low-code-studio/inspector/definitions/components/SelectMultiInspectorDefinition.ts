import { ChoiceInspectorDefinitionBase } from '../base/ChoiceInspectorDefinitionBase';

export class SelectMultiInspectorDefinition extends ChoiceInspectorDefinitionBase {
  public constructor() {
    super('select.multi');
    this.property({ path: 'props.multiple', section: 'content', order: 40, editor: 'boolean', valueType: 'boolean', defaultValue: true, labelKey: 'lowCode.inspector.choice.multiple.label', helpKey: 'lowCode.inspector.choice.multiple.help', fallbackLabel: 'Multiple', runtimeConsumer: 'runtime.choice.multiple' });
  }
}
