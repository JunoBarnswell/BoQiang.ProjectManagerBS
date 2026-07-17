import { MediaInspectorDefinitionBase } from '../base/MediaInspectorDefinitionBase';

export class MediaAudioInspectorDefinition extends MediaInspectorDefinitionBase {
  public constructor() {
    super('media.audio');
this.property({ path: 'props.controls', section: 'content', order: 30, editor: 'boolean', valueType: 'boolean', defaultValue: true, labelKey: 'lowCode.inspector.media.controls.label', helpKey: 'lowCode.inspector.media.controls.help', fallbackLabel: 'Controls', runtimeConsumer: 'runtime.media.controls' });
    this.property({ path: 'props.autoplay', section: 'content', order: 40, editor: 'boolean', valueType: 'boolean', defaultValue: false, labelKey: 'lowCode.inspector.media.autoplay.label', helpKey: 'lowCode.inspector.media.autoplay.help', fallbackLabel: 'Autoplay', runtimeConsumer: 'runtime.media.autoplay' });
  }
}
