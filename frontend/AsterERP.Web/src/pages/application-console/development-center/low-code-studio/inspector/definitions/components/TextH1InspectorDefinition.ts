import { TextInspectorDefinitionBase } from '../base/TextInspectorDefinitionBase';

export class TextH1InspectorDefinition extends TextInspectorDefinitionBase {
  public constructor() {
    super('text.h1');
    this.onlyInherited();
  }
}
