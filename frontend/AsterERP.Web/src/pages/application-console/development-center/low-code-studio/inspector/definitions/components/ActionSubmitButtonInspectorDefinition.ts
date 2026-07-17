import { ActionInspectorDefinitionBase } from '../base/ActionInspectorDefinitionBase';

export class ActionSubmitButtonInspectorDefinition extends ActionInspectorDefinitionBase {
  public constructor() {
    super('action.submitButton');
    this.property({ path: 'props.buttonType', section: 'interaction', order: 40, editor: 'select', valueType: 'string', defaultValue: 'submit', labelKey: 'lowCode.inspector.action.type.label', helpKey: 'lowCode.inspector.action.type.help', fallbackLabel: 'Button type', runtimeConsumer: 'runtime.action.type', options: [{ label: 'Submit', value: 'submit' }] });
    this.onlyInherited();
  }
}
