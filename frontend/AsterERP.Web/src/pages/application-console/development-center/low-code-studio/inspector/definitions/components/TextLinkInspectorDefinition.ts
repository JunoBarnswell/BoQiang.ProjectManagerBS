import { TextInspectorDefinitionBase } from '../base/TextInspectorDefinitionBase';

export class TextLinkInspectorDefinition extends TextInspectorDefinitionBase {
  public constructor() {
    super('text.link');
    this.property({ path: 'props.href', section: 'content', order: 20, editor: 'text', valueType: 'string', defaultValue: '#', labelKey: 'lowCode.inspector.text.link.href.label', helpKey: 'lowCode.inspector.text.link.href.help', fallbackLabel: 'Link URL', bindable: true, acceptedSources: ['page', 'component', 'variable'], runtimeConsumer: 'runtime.component.props.href' });
    this.property({ path: 'props.target', section: 'content', order: 30, editor: 'select', valueType: 'string', defaultValue: '_self', labelKey: 'lowCode.inspector.text.link.target.label', helpKey: 'lowCode.inspector.text.link.target.help', fallbackLabel: 'Link target', runtimeConsumer: 'runtime.component.props.target', options: [{ label: 'Same window', value: '_self' }, { label: 'New window', value: '_blank' }, { label: 'Parent frame', value: '_parent' }, { label: 'Top frame', value: '_top' }] });
    this.onlyInherited();
  }
}
