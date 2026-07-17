import { DataComponentInspectorDefinitionBase } from '../base/DataComponentInspectorDefinitionBase';

export class MetricMeterInspectorDefinition extends DataComponentInspectorDefinitionBase {
  public constructor() {
    super('metric.meter');
this.property({ path: 'props.min', section: 'content', order: 30, editor: 'number', valueType: 'number', defaultValue: 0, labelKey: 'lowCode.inspector.metric.min.label', helpKey: 'lowCode.inspector.metric.min.help', fallbackLabel: 'Minimum', runtimeConsumer: 'runtime.metric.min' });
    this.property({ path: 'props.max', section: 'content', order: 40, editor: 'number', valueType: 'number', defaultValue: 100, labelKey: 'lowCode.inspector.metric.max.label', helpKey: 'lowCode.inspector.metric.max.help', fallbackLabel: 'Maximum', runtimeConsumer: 'runtime.metric.max' });
    this.property({ path: 'props.low', section: 'content', order: 50, editor: 'number', valueType: 'number', defaultValue: 20, labelKey: 'lowCode.inspector.metric.low.label', helpKey: 'lowCode.inspector.metric.low.help', fallbackLabel: 'Low', runtimeConsumer: 'runtime.metric.low' });
    this.property({ path: 'props.high', section: 'content', order: 60, editor: 'number', valueType: 'number', defaultValue: 80, labelKey: 'lowCode.inspector.metric.high.label', helpKey: 'lowCode.inspector.metric.high.help', fallbackLabel: 'High', runtimeConsumer: 'runtime.metric.high' });
    this.property({ path: 'props.optimum', section: 'content', order: 70, editor: 'number', valueType: 'number', defaultValue: 60, labelKey: 'lowCode.inspector.metric.optimum.label', helpKey: 'lowCode.inspector.metric.optimum.help', fallbackLabel: 'Optimum', runtimeConsumer: 'runtime.metric.optimum' });
  }
}
