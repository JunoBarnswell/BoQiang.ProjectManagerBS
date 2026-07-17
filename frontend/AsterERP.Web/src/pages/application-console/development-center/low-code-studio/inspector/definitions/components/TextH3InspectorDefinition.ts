import { TextInspectorDefinitionBase } from '../base/TextInspectorDefinitionBase';

export class TextH3InspectorDefinition extends TextInspectorDefinitionBase {
  public constructor() {
    super('text.h3');
    this.onlyInherited();
  }
}
