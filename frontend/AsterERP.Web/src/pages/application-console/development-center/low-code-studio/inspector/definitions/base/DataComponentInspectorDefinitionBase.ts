import { VisualComponentInspectorDefinitionBase } from './VisualComponentInspectorDefinitionBase';

export abstract class DataComponentInspectorDefinitionBase extends VisualComponentInspectorDefinitionBase {
  protected constructor(componentType: string) {
    super(componentType);
    this.property({ path: 'bindings.data.field', section: 'data', order: 10, editor: 'text', valueType: 'string', defaultValue: '', labelKey: 'lowCode.inspector.common.dataField.label', helpKey: 'lowCode.inspector.common.dataField.help', fallbackLabel: 'Data field', bindable: true, acceptedSources: ['page', 'component', 'variable', 'dataset'], responsive: { enabled: false, mode: 'inherit' }, runtimeConsumer: 'runtime.binding.data' });
    this.property({ path: 'props.dataSource', section: 'data', order: 20, editor: 'json', valueType: 'object', defaultValue: {}, labelKey: 'lowCode.inspector.data.dataSource.label', helpKey: 'lowCode.inspector.data.dataSource.help', fallbackLabel: 'Data source', bindable: true, acceptedSources: ['page', 'component', 'variable', 'dataset'], runtimeConsumer: 'runtime.data.source' });
  }
}
