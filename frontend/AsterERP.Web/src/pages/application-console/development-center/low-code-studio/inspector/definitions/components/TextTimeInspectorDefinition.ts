import { TextInspectorDefinitionBase } from '../base/TextInspectorDefinitionBase';

export class TextTimeInspectorDefinition extends TextInspectorDefinitionBase {
  public constructor() {
    super('text.time');
    this.onlyInherited();
  }
}
