import { describe, expect, it } from 'vitest';

import { ChartBasicInspectorDefinition } from './ChartBasicInspectorDefinition';
import { ReportDataTableInspectorDefinition } from './ReportDataTableInspectorDefinition';
import { TableSemanticInspectorDefinition } from './TableSemanticInspectorDefinition';

describe('chart and table inspector runtime contracts', () => {
  it('declares every chart prop consumed by the chart runtime', () => {
    const definition = new ChartBasicInspectorDefinition().build();

    expect(properties(definition)).toMatchObject({
      'props.axis': { editor: 'json', runtimeConsumer: 'runtime.chart.axis', valueType: 'object' },
      'props.chartType': { defaultValue: 'bar', runtimeConsumer: 'runtime.chart.type', valueType: 'string' },
      'props.emptyState': { defaultValue: 'No chart data', runtimeConsumer: 'runtime.chart.emptyState' },
      'props.errorState': { runtimeConsumer: 'runtime.chart.errorState' },
      'props.legend': { defaultValue: true, runtimeConsumer: 'runtime.chart.legend', valueType: 'boolean' },
      'props.loadingState': { runtimeConsumer: 'runtime.chart.loadingState' },
      'props.series': { editor: 'json', runtimeConsumer: 'runtime.chart.series', valueType: 'array' },
      'props.tooltip': { defaultValue: true, runtimeConsumer: 'runtime.chart.tooltip', valueType: 'boolean' }
    });
  });

  it('keeps data-table collections typed as arrays while exposing editing consumers', () => {
    const definition = new ReportDataTableInspectorDefinition().build();
    const tableProperties = properties(definition);

    expect(tableProperties['props.rowActions']).toMatchObject({ editor: 'json', valueType: 'array', runtimeConsumer: 'runtime.table.rowActions' });
    expect(tableProperties['props.loadingState']).toMatchObject({ defaultValue: 'Loading…', runtimeConsumer: 'runtime.table.loadingState' });
    expect(tableProperties['props.emptyState']).toMatchObject({ defaultValue: '暂无数据', runtimeConsumer: 'runtime.table.emptyState' });
    expect(tableProperties['props.pageSize']).toMatchObject({ defaultValue: 20, runtimeConsumer: 'runtime.table.pageSize', valueType: 'number' });
    expect(tableProperties['props.editing']).toMatchObject({ editor: 'json', valueType: 'object', runtimeConsumer: 'runtime.table.editing' });
    expect(tableProperties['props.editable']).toMatchObject({ defaultValue: false, runtimeConsumer: 'runtime.table.editing' });
    expect(tableProperties['props.commitOnBlur']).toMatchObject({ defaultValue: true, runtimeConsumer: 'runtime.table.editing' });
    expect(tableProperties['props.keyField']).toMatchObject({ defaultValue: 'id', runtimeConsumer: 'runtime.table.editing' });
  });

  it('declares semantic table accessibility and state consumers', () => {
    const definition = new TableSemanticInspectorDefinition().build();
    expect(properties(definition)).toMatchObject({
      'props.caption': { runtimeConsumer: 'runtime.table.caption', valueType: 'string' },
      'props.emptyState': { defaultValue: 'No data', runtimeConsumer: 'runtime.table.emptyState' },
      'props.errorState': { runtimeConsumer: 'runtime.table.errorState' },
      'props.loadingState': { runtimeConsumer: 'runtime.table.loadingState' }
    });
  });
});

function properties(definition: ReturnType<ChartBasicInspectorDefinition['build']>): Record<string, ReturnType<ChartBasicInspectorDefinition['build']>['properties'][number]> {
  return Object.fromEntries(definition.properties.map((property) => [property.path, property]));
}
