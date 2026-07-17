import { MediaInspectorDefinitionBase } from '../base/MediaInspectorDefinitionBase';

export class MediaSvgInspectorDefinition extends MediaInspectorDefinitionBase {
  public constructor() {
    super('media.svg');
this.property({ path: 'props.source', section: 'content', order: 30, editor: 'textarea', valueType: 'string', defaultValue: '', labelKey: 'lowCode.inspector.media.source.label', helpKey: 'lowCode.inspector.media.source.help', fallbackLabel: 'SVG source', runtimeConsumer: 'runtime.media.source' });
  }
}
