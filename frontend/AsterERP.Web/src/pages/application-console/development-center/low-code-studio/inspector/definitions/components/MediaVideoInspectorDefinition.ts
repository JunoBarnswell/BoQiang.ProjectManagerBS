import { MediaInspectorDefinitionBase } from '../base/MediaInspectorDefinitionBase';

export class MediaVideoInspectorDefinition extends MediaInspectorDefinitionBase {
  public constructor() {
    super('media.video');
this.property({ path: 'props.controls', section: 'content', order: 30, editor: 'boolean', valueType: 'boolean', defaultValue: true, labelKey: 'lowCode.inspector.media.controls.label', helpKey: 'lowCode.inspector.media.controls.help', fallbackLabel: 'Controls', runtimeConsumer: 'runtime.media.controls' });
    this.property({ path: 'props.poster', section: 'content', order: 40, editor: 'text', valueType: 'string', defaultValue: '', labelKey: 'lowCode.inspector.media.poster.label', helpKey: 'lowCode.inspector.media.poster.help', fallbackLabel: 'Poster', runtimeConsumer: 'runtime.media.poster' });
  }
}
