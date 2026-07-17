import { MediaInspectorDefinitionBase } from '../base/MediaInspectorDefinitionBase';

export class MediaFigureInspectorDefinition extends MediaInspectorDefinitionBase {
  public constructor() {
    super('media.figure');
    this.onlyInherited();
  }
}
