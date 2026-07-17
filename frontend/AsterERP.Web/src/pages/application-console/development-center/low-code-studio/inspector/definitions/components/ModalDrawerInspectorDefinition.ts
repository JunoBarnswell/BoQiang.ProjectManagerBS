import { ModalInspectorDefinitionBase } from '../base/ModalInspectorDefinitionBase';

export class ModalDrawerInspectorDefinition extends ModalInspectorDefinitionBase {
  public constructor() {
    super('modal.drawer');
this.property({ path: 'props.open', section: 'interaction', order: 30, editor: 'boolean', valueType: 'boolean', defaultValue: false, labelKey: 'lowCode.inspector.modal.open.label', helpKey: 'lowCode.inspector.modal.open.help', fallbackLabel: 'Open', bindable: true, acceptedSources: ['page', 'component', 'variable'], runtimeConsumer: 'runtime.modal.open' });
    this.property({ path: 'props.placement', section: 'appearance', order: 40, editor: 'select', valueType: 'string', defaultValue: 'right', labelKey: 'lowCode.inspector.modal.placement.label', helpKey: 'lowCode.inspector.modal.placement.help', fallbackLabel: 'Placement', runtimeConsumer: 'runtime.modal.placement', options: [{ label: 'Left', value: 'left' }, { label: 'Right', value: 'right' }, { label: 'Top', value: 'top' }, { label: 'Bottom', value: 'bottom' }] });
  }
}
