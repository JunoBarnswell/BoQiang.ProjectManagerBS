import { LayoutDocumentInspectorDefinitionBase } from '../base/LayoutDocumentInspectorDefinitionBase';

export class DocumentTemplateInspectorDefinition extends LayoutDocumentInspectorDefinitionBase {
  public constructor() {
    super('document.template');
    this.property({ path: 'layout.display', section: 'layout', order: 240, editor: 'select', valueType: 'string', defaultValue: 'block', labelKey: 'lowCode.inspector.layout.display.label', helpKey: 'lowCode.inspector.layout.display.help', fallbackLabel: 'Display', runtimeConsumer: 'runtime.component.layout', options: [{ label: 'Block', value: 'block' }, { label: 'Flex', value: 'flex' }, { label: 'Grid', value: 'grid' }, { label: 'Constraints', value: 'constraints' }] });
    this.property({ path: 'layout.position', section: 'layout', order: 250, editor: 'select', valueType: 'string', defaultValue: 'relative', labelKey: 'lowCode.inspector.layout.position.label', helpKey: 'lowCode.inspector.layout.position.help', fallbackLabel: 'Position', runtimeConsumer: 'runtime.component.layout', options: [{ label: 'Static', value: 'static' }, { label: 'Relative', value: 'relative' }, { label: 'Absolute', value: 'absolute' }] });
  }
}
