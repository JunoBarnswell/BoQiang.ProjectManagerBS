import { MediaInspectorDefinitionBase } from '../base/MediaInspectorDefinitionBase';

export class MediaSourceInspectorDefinition extends MediaInspectorDefinitionBase {
  public constructor() {
    super('media.source');
    this.onlyInherited();
  }
}
