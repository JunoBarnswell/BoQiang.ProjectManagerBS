import { MediaInspectorDefinitionBase } from '../base/MediaInspectorDefinitionBase';

export class MediaIframeInspectorDefinition extends MediaInspectorDefinitionBase {
  public constructor() {
    super('media.iframe');
this.property({ path: 'props.sandbox', section: 'advanced', order: 30, editor: 'select', valueType: 'string', defaultValue: 'allow-scripts', labelKey: 'lowCode.inspector.media.sandbox.label', helpKey: 'lowCode.inspector.media.sandbox.help', fallbackLabel: 'Sandbox profile', runtimeConsumer: 'runtime.media.sandbox', options: [{ label: 'Scripts', value: 'allow-scripts' }, { label: 'Same origin and scripts', value: 'allow-same-origin allow-scripts' }] });
  }
}
