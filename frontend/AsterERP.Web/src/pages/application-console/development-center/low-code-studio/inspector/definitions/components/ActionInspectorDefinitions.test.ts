import { describe, expect, it } from 'vitest';

import { ActionButtonInspectorDefinition } from './ActionButtonInspectorDefinition';
import { ActionImageButtonInspectorDefinition } from './ActionImageButtonInspectorDefinition';
import { ActionResetButtonInspectorDefinition } from './ActionResetButtonInspectorDefinition';
import { ActionSubmitButtonInspectorDefinition } from './ActionSubmitButtonInspectorDefinition';
import { BusinessPrintActionInspectorDefinition } from './BusinessPrintActionInspectorDefinition';
import { IntegrationApiCallInspectorDefinition } from './IntegrationApiCallInspectorDefinition';
import { WorkflowActionsInspectorDefinition } from './WorkflowActionsInspectorDefinition';

describe('Action inspector definitions', () => {
  it('keeps shared action and interactive properties inherited', () => {
    const definitions = [
      new ActionButtonInspectorDefinition(),
      new ActionImageButtonInspectorDefinition(),
      new ActionResetButtonInspectorDefinition(),
      new ActionSubmitButtonInspectorDefinition(),
      new BusinessPrintActionInspectorDefinition(),
      new IntegrationApiCallInspectorDefinition(),
      new WorkflowActionsInspectorDefinition(),
    ];

    for (const definition of definitions) {
      const contract = definition.build();

      expect(contract.onlyInherited).toBe(true);
      expect(contract.properties.filter((property) => property.path === 'props.text')).toHaveLength(1);
      expect(contract.properties.filter((property) => property.path === 'props.loading')).toHaveLength(1);
      expect(contract.properties.filter((property) => property.path === 'props.disabled')).toHaveLength(1);
      expect(contract.properties.find((property) => property.path === 'props.text')).toMatchObject({
        runtimeConsumer: 'runtime.action.label',
      });
      expect(contract.properties.find((property) => property.path === 'props.loading')).toMatchObject({
        runtimeConsumer: 'runtime.component.loading',
      });
      expect(contract.properties.find((property) => property.path === 'props.disabled')).toMatchObject({
        runtimeConsumer: 'runtime.component.props.disabled',
      });
    }
  });

  it.each([
    [new ActionButtonInspectorDefinition(), 'action.button', 'button', ['button', 'submit', 'reset']],
    [new ActionImageButtonInspectorDefinition(), 'action.imageButton', 'button', ['button', 'submit', 'reset']],
    [new ActionResetButtonInspectorDefinition(), 'action.resetButton', 'reset', ['reset']],
    [new ActionSubmitButtonInspectorDefinition(), 'action.submitButton', 'submit', ['submit']],
    [new BusinessPrintActionInspectorDefinition(), 'business.printAction', 'button', ['button']],
    [new IntegrationApiCallInspectorDefinition(), 'integration.apiCall', 'button', ['button']],
    [new WorkflowActionsInspectorDefinition(), 'workflow.actions', 'button', ['button']],
  ] as const)('describes %s action type runtime inputs', (definition, componentType, defaultType, optionValues) => {
    const contract = definition.build();
    const type = contract.properties.find((property) => property.path === 'props.buttonType');

    expect(contract.componentType).toBe(componentType);
    expect(type).toMatchObject({
      defaultValue: defaultType,
      editor: 'select',
      runtimeConsumer: 'runtime.action.type',
      valueType: 'string',
    });
    expect(type?.options?.map((option) => option.value)).toEqual(optionValues);
    expect(contract.properties.some((property) => property.path === 'props.target')).toBe(false);
  });
});
