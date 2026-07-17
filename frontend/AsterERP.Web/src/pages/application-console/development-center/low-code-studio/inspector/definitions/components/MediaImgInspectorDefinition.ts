import { MediaInspectorDefinitionBase } from '../base/MediaInspectorDefinitionBase';

export class MediaImgInspectorDefinition extends MediaInspectorDefinitionBase {
  public constructor() {
    super('media.img');
    this.onlyInherited();
  }
}
