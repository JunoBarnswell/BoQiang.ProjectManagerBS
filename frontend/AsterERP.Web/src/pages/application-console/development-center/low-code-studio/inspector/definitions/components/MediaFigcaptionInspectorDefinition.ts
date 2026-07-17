import { MediaInspectorDefinitionBase } from '../base/MediaInspectorDefinitionBase';

export class MediaFigcaptionInspectorDefinition extends MediaInspectorDefinitionBase {
  public constructor() {
    super('media.figcaption');
    this.onlyInherited();
  }
}
