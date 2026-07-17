import { ActionInspectorDefinitionBase } from '../base/ActionInspectorDefinitionBase';

export class ActionImageButtonInspectorDefinition extends ActionInspectorDefinitionBase {
  public constructor() {
    super('action.imageButton');
    this.property({ path: 'props.buttonType', section: 'interaction', order: 40, editor: 'select', valueType: 'string', defaultValue: 'button', labelKey: 'lowCode.inspector.action.type.label', helpKey: 'lowCode.inspector.action.type.help', fallbackLabel: 'Button type', runtimeConsumer: 'runtime.action.type', options: [{ label: 'Button', value: 'button' }, { label: 'Submit', value: 'submit' }, { label: 'Reset', value: 'reset' }] });
    this.onlyInherited();
  }
}
