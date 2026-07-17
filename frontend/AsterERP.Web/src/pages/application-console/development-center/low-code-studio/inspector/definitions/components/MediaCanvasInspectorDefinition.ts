import { MediaInspectorDefinitionBase } from '../base/MediaInspectorDefinitionBase';

export class MediaCanvasInspectorDefinition extends MediaInspectorDefinitionBase {
  public constructor() {
    super('media.canvas');
this.property({ path: 'props.width', section: 'content', order: 30, editor: 'number', valueType: 'number', defaultValue: 640, labelKey: 'lowCode.inspector.media.width.label', helpKey: 'lowCode.inspector.media.width.help', fallbackLabel: 'Canvas width', runtimeConsumer: 'runtime.media.width', validation: { valueType: 'number', min: 1 } });
  }
}
