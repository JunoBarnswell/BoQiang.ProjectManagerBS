import { TextInspectorDefinitionBase } from '../base/TextInspectorDefinitionBase';

export class TextH2InspectorDefinition extends TextInspectorDefinitionBase {
  public constructor() {
    super('text.h2');
    this.onlyInherited();
  }
}
