import { ContainerInspectorDefinitionBase } from '../base/ContainerInspectorDefinitionBase';

export class ListUlInspectorDefinition extends ContainerInspectorDefinitionBase {
  public constructor() {
    super('list.ul');
this.property({ path: 'props.marker', section: 'appearance', order: 30, editor: 'select', valueType: 'string', defaultValue: 'disc', labelKey: 'lowCode.inspector.list.marker.label', helpKey: 'lowCode.inspector.list.marker.help', fallbackLabel: 'Marker', runtimeConsumer: 'runtime.list.marker', options: [{ label: 'Disc', value: 'disc' }, { label: 'Circle', value: 'circle' }, { label: 'Square', value: 'square' }] });
  }
}
