import { TextInspectorDefinitionBase } from '../base/TextInspectorDefinitionBase';

export class TextH5InspectorDefinition extends TextInspectorDefinitionBase {
  public constructor() {
    super('text.h5');
    this.onlyInherited();
  }
}
