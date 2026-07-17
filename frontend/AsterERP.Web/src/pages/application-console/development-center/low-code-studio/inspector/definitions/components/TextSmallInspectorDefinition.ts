import { TextInspectorDefinitionBase } from '../base/TextInspectorDefinitionBase';

export class TextSmallInspectorDefinition extends TextInspectorDefinitionBase {
  public constructor() {
    super('text.small');
    this.onlyInherited();
  }
}
