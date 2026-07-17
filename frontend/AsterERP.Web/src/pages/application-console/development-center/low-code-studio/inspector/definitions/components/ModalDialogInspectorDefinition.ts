import { ModalInspectorDefinitionBase } from '../base/ModalInspectorDefinitionBase';

export class ModalDialogInspectorDefinition extends ModalInspectorDefinitionBase {
  public constructor() {
    super('modal.dialog');
this.property({ path: 'props.open', section: 'interaction', order: 30, editor: 'boolean', valueType: 'boolean', defaultValue: false, labelKey: 'lowCode.inspector.modal.open.label', helpKey: 'lowCode.inspector.modal.open.help', fallbackLabel: 'Open', bindable: true, acceptedSources: ['page', 'component', 'variable'], runtimeConsumer: 'runtime.modal.open' });
    this.property({ path: 'props.mask', section: 'appearance', order: 40, editor: 'boolean', valueType: 'boolean', defaultValue: true, labelKey: 'lowCode.inspector.modal.mask.label', helpKey: 'lowCode.inspector.modal.mask.help', fallbackLabel: 'Mask', runtimeConsumer: 'runtime.modal.mask' });
  }
}
