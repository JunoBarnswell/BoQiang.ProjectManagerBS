import { MediaInspectorDefinitionBase } from '../base/MediaInspectorDefinitionBase';

export class MediaMathInspectorDefinition extends MediaInspectorDefinitionBase {
  public constructor() {
    super('media.math');
    this.onlyInherited();
  }
}
