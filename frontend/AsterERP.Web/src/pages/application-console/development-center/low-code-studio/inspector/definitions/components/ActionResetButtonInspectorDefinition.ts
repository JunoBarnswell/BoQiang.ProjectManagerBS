import { ActionInspectorDefinitionBase } from '../base/ActionInspectorDefinitionBase';

export class ActionResetButtonInspectorDefinition extends ActionInspectorDefinitionBase {
  public constructor() {
    super('action.resetButton');
    this.property({ path: 'props.buttonType', section: 'interaction', order: 40, editor: 'select', valueType: 'string', defaultValue: 'reset', labelKey: 'lowCode.inspector.action.type.label', helpKey: 'lowCode.inspector.action.type.help', fallbackLabel: 'Button type', runtimeConsumer: 'runtime.action.type', options: [{ label: 'Reset', value: 'reset' }] });
    this.onlyInherited();
  }
}
