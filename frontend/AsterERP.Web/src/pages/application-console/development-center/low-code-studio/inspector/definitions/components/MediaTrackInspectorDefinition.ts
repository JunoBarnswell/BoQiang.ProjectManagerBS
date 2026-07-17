import { MediaInspectorDefinitionBase } from '../base/MediaInspectorDefinitionBase';

export class MediaTrackInspectorDefinition extends MediaInspectorDefinitionBase {
  public constructor() {
    super('media.track');
    this.onlyInherited();
  }
}
