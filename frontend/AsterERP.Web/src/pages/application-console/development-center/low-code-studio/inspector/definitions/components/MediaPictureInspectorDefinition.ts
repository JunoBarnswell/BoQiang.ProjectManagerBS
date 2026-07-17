import { MediaInspectorDefinitionBase } from '../base/MediaInspectorDefinitionBase';

export class MediaPictureInspectorDefinition extends MediaInspectorDefinitionBase {
  public constructor() {
    super('media.picture');
    this.onlyInherited();
  }
}
