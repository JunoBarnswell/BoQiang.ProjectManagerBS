import { DataComponentInspectorDefinitionBase } from '../base/DataComponentInspectorDefinitionBase';

export class MetricProgressInspectorDefinition extends DataComponentInspectorDefinitionBase {
  public constructor() {
    super('metric.progress');
this.property({ path: 'props.max', section: 'content', order: 30, editor: 'number', valueType: 'number', defaultValue: 100, labelKey: 'lowCode.inspector.metric.max.label', helpKey: 'lowCode.inspector.metric.max.help', fallbackLabel: 'Maximum', runtimeConsumer: 'runtime.metric.max' });
    this.property({ path: 'props.indeterminate', section: 'content', order: 40, editor: 'boolean', valueType: 'boolean', defaultValue: false, labelKey: 'lowCode.inspector.metric.indeterminate.label', helpKey: 'lowCode.inspector.metric.indeterminate.help', fallbackLabel: 'Indeterminate', runtimeConsumer: 'runtime.metric.indeterminate' });
  }
}
