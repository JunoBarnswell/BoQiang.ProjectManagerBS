import { describe, expect, it } from "vitest";

import {
  RUNTIME_CAPABILITY_CONTRACT,
  RUNTIME_INSPECTOR_CONTRACT,
} from "../../../../../../runtime-kernel/runtime-contract/RuntimeCapabilityContract";
import { ComponentInspectorDefinitionBase } from "../definitions/base/ComponentInspectorDefinitionBase";

import { ComponentInspectorRegistry } from "./ComponentInspectorRegistry";

describe("ComponentInspectorRegistry", () => {
  it("registers one concrete definition for every canonical component type", () => {
    const types = RUNTIME_CAPABILITY_CONTRACT.components;
    expect(RUNTIME_INSPECTOR_CONTRACT.types()).toHaveLength(types.length);
    expect(new Set(RUNTIME_INSPECTOR_CONTRACT.types()).size).toBe(types.length);
    expect(RUNTIME_INSPECTOR_CONTRACT.validate(types)).toEqual([]);
    for (const type of types) {
      const definition = RUNTIME_INSPECTOR_CONTRACT.get(type);
      expect(definition?.componentType).toBe(type);
      expect(definition?.ownerType).toBe(type);
      expect(typeof definition?.onlyInherited).toBe("boolean");
      expect(definition?.properties.every((property) => property.ownerType === type)).toBe(true);
    }
  });

  it("rejects duplicate paths, missing runtime consumers, and invalid defaults", () => {
    class InvalidDefinition extends ComponentInspectorDefinitionBase {
      public constructor() {
        super("test.invalid");
        this.property({
          path: "props.value",
          section: "content",
          order: 10,
          editor: "text",
          valueType: "string",
          defaultValue: 123,
          labelKey: "test.value.label",
          helpKey: "test.value.help",
          fallbackLabel: "Value",
          runtimeConsumer: "",
        });
        this.property({
          path: "props.value",
          section: "content",
          order: 20,
          editor: "text",
          valueType: "string",
          defaultValue: "",
          labelKey: "test.value.label",
          helpKey: "test.value.help",
          fallbackLabel: "Value",
          runtimeConsumer: "runtime.test.value",
        });
      }
    }

    const registry = new ComponentInspectorRegistry();
    registry.register(new InvalidDefinition());
    expect(registry.validate().map((item) => item.code)).toEqual(
      expect.arrayContaining(["duplicatePath", "missingRuntimeConsumer", "invalidDefaultValue"]),
    );
    expect(() => registry.assertValid()).toThrow("duplicatePath");
  });
});
