import type { InspectorEditorKind } from "../../contract/InspectorEditorKind";
import type {
  ComponentInspectorDefinition,
  InspectorBatchPolicy,
  InspectorBindingPolicy,
  InspectorCondition,
  InspectorAccessibilityMetadata,
  InspectorPropertyDescriptor,
  InspectorResetPolicy,
  InspectorResponsivePolicy,
  InspectorValidation,
  InspectorValueType,
} from "../../contract/InspectorPropertyDescriptor";
import type { InspectorSectionDefinition } from "../../contract/InspectorSectionDefinition";

export interface InspectorPropertyInput {
  semanticId?: string;
  path: string;
  section: string;
  order: number;
  editor: InspectorEditorKind;
  valueType: InspectorValueType;
  defaultValue: unknown;
  labelKey: string;
  helpKey: string;
  fallbackLabel: string;
  bindable?: boolean;
  acceptedSources?: readonly string[];
  bindingPolicy?: InspectorBindingPolicy;
  responsive?: InspectorResponsivePolicy;
  batchPolicy?: InspectorBatchPolicy;
  visibleWhen?: InspectorCondition;
  enabledWhen?: InspectorCondition;
  validation?: InspectorValidation;
  resetPolicy?: InspectorResetPolicy;
  runtimeConsumer: string;
  unit?: string;
  accessibility?: InspectorAccessibilityMetadata;
  options?: readonly { label: string; value: string }[];
  placeholder?: string;
}

export abstract class ComponentInspectorDefinitionBase {
  private readonly sectionsValue: InspectorSectionDefinition[] = [];
  private readonly propertiesValue: InspectorPropertyDescriptor[] = [];
  private onlyInheritedValue = false;

  protected constructor(public readonly componentType: string) {
    this.section("content", "lowCode.pageStudio.sectionContent", 10);
    this.section("layout", "lowCode.pageStudio.sectionLayout", 20);
    this.section("appearance", "lowCode.pageStudio.sectionAppearance", 30);
    this.section("data", "lowCode.pageStudio.sectionData", 40);
    this.section("interaction", "lowCode.pageStudio.sectionInteraction", 50);
    this.section("advanced", "lowCode.pageStudio.sectionAdvanced", 90);
    this.property({
      path: "props.visible",
      section: "interaction",
      order: 10,
      editor: "boolean",
      valueType: "boolean",
      defaultValue: true,
      labelKey: "lowCode.inspector.common.visible.label",
      helpKey: "lowCode.inspector.common.visible.help",
      fallbackLabel: "Visible",
      bindable: true,
      acceptedSources: ["page", "component", "variable"],
      responsive: { enabled: false, mode: "inherit" },
      runtimeConsumer: "runtime.component.visible",
    });
    this.property({
      path: "permission.code",
      section: "advanced",
      order: 10,
      editor: "text",
      valueType: "string",
      defaultValue: "",
      labelKey: "lowCode.inspector.common.permission.label",
      helpKey: "lowCode.inspector.common.permission.help",
      fallbackLabel: "Required permission",
      runtimeConsumer: "runtime.permission.code",
      batchPolicy: "readOnly",
      responsive: { enabled: false, mode: "inherit" },
    });
  }

  protected section(id: string, labelKey: string, order: number): this {
    if (!this.sectionsValue.some((item) => item.id === id)) this.sectionsValue.push({ id, labelKey, order });
    return this;
  }

  protected property(input: InspectorPropertyInput): this {
    const descriptor: InspectorPropertyDescriptor = {
      ...input,
      id: `${this.componentType}:${input.path}`,
      semanticId: input.semanticId ?? input.path,
      ownerType: this.componentType,
      bindable: input.bindable ?? false,
      acceptedSources: [...(input.acceptedSources ?? [])],
      bindingPolicy: input.bindingPolicy ?? {
        enabled: input.bindable ?? false,
        acceptedSources: [...(input.acceptedSources ?? [])],
      },
      responsive: input.responsive ?? defaultResponsivePolicy(input.path),
      batchPolicy: input.batchPolicy ?? "editable",
      validation: input.validation ?? { valueType: input.valueType },
      resetPolicy: input.resetPolicy ?? "default",
    };
    this.propertiesValue.push(descriptor);
    return this;
  }

  protected onlyInherited(): this {
    this.onlyInheritedValue = true;
    return this;
  }

  public build(): ComponentInspectorDefinition {
    return {
      componentType: this.componentType,
      ownerType: this.componentType,
      onlyInherited: this.onlyInheritedValue,
      sections: [...this.sectionsValue].sort((left, right) => left.order - right.order),
      properties: [...this.propertiesValue].sort(
        (left, right) => left.order - right.order || left.path.localeCompare(right.path),
      ),
    };
  }
}

function defaultResponsivePolicy(path: string): InspectorResponsivePolicy {
  const scope = path.split(".")[0];
  return scope === "layout" || scope === "style"
    ? { enabled: true, mode: "override" }
    : { enabled: false, mode: "inherit" };
}
