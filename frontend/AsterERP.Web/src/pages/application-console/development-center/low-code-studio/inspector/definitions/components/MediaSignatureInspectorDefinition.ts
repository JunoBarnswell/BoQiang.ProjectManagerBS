import { MediaInspectorDefinitionBase } from '../base/MediaInspectorDefinitionBase';

export class MediaSignatureInspectorDefinition extends MediaInspectorDefinitionBase {
  public constructor() {
    super('media.signature');
this.property({ path: 'props.penColor', section: 'appearance', order: 30, editor: 'color', valueType: 'string', defaultValue: '#111827', labelKey: 'lowCode.inspector.media.penColor.label', helpKey: 'lowCode.inspector.media.penColor.help', fallbackLabel: 'Pen color', runtimeConsumer: 'runtime.signature.penColor' });
  }
}
