import { MediaInspectorDefinitionBase } from '../base/MediaInspectorDefinitionBase';

export class MediaImageUploadInspectorDefinition extends MediaInspectorDefinitionBase {
  public constructor() {
    super('media.imageUpload');
this.property({ path: 'props.accept', section: 'content', order: 30, editor: 'text', valueType: 'string', defaultValue: 'image/*', labelKey: 'lowCode.inspector.upload.accept.label', helpKey: 'lowCode.inspector.upload.accept.help', fallbackLabel: 'Accepted types', runtimeConsumer: 'runtime.upload.accept' });
    this.property({ path: 'props.multiple', section: 'content', order: 40, editor: 'boolean', valueType: 'boolean', defaultValue: false, labelKey: 'lowCode.inspector.upload.multiple.label', helpKey: 'lowCode.inspector.upload.multiple.help', fallbackLabel: 'Multiple', runtimeConsumer: 'runtime.upload.multiple' });
  }
}
