import type { ReactNode } from 'react';

import type { RuntimeComponentRenderContext } from './RuntimeComponentTypes';
import { applyRuntimeNodePresentation } from './RuntimeNodePresentation';

interface ChartPoint {
  label: string;
  value: number;
}

interface ChartSeries {
  data: ChartPoint[];
  name: string;
}

interface ChartAxis {
  xKey: string;
  yKey: string;
  xLabel?: string;
  yLabel?: string;
}

export function hasChartRuntimeRenderer(type: string): boolean {
  return type === 'chart.basic';
}

export function renderChartRuntime(context: RuntimeComponentRenderContext): ReactNode {
  const chartType = readChartType(context.props.chartType);
  const axis = readAxis(context.props.axis);
  const series = readSeries(context.props.series, context.props.data ?? context.value, axis);
  const error = context.props.error ?? context.bindings?.error;
  const title = String(context.props.title ?? context.title);
  const legendVisible = readBooleanOption(context.props.legend, 'visible', true);
  const tooltipEnabled = readBooleanOption(context.props.tooltip, 'enabled', true);
  return applyRuntimeNodePresentation(context, <figure aria-label={title} className="grid gap-2 rounded border border-slate-200 bg-white p-3" data-chart-type={chartType}>
    <figcaption className="text-sm font-semibold text-slate-700">{title}</figcaption>
    {context.loading ? <p className="text-xs text-slate-500" role="status">{String(context.props.loadingState ?? 'Loading')}</p> : error ? <p className="text-xs text-red-600" role="alert">{String(context.props.errorState ?? error)}</p> : series.every((item) => item.data.length === 0) ? <p className="text-xs text-slate-500">{String(context.props.emptyState ?? 'No chart data')}</p> : <div className="grid gap-2">
      <div aria-label={describeChart(series, axis)} role="img">
        <svg className="h-48 w-full" role="presentation" viewBox="0 0 640 240">
          <text x="320" y="235" textAnchor="middle">{axis.xLabel ?? axis.xKey}</text>
          <text transform="translate(15 120) rotate(-90)" textAnchor="middle">{axis.yLabel ?? axis.yKey}</text>
          {renderSeries(chartType, series, tooltipEnabled)}
        </svg>
      </div>
      {legendVisible ? <ul aria-label="Legend" className="flex flex-wrap gap-3 text-xs text-slate-600">{series.map((item) => <li key={item.name}>{item.name}</li>)}</ul> : null}
    </div>}
  </figure>);
}

function renderSeries(chartType: string, series: ChartSeries[], tooltipEnabled: boolean): ReactNode {
  const visible = series.filter((item) => item.data.length > 0);
  const maximum = Math.max(1, ...visible.flatMap((item) => item.data.map((point) => point.value)));
  const width = 560;
  const pointX = (index: number, count: number) => 48 + (count <= 1 ? width / 2 : (index / (count - 1)) * width);
  return visible.map((item, seriesIndex) => {
    const points = item.data;
    const color = ['#4f46e5', '#0891b2', '#16a34a', '#ea580c'][seriesIndex % 4];
    if (chartType === 'bar' || chartType === 'column' || chartType === 'pie') {
      return <g key={item.name}>{points.map((point, index) => {
        const barWidth = Math.max(12, width / Math.max(points.length, 1) - 8);
        const x = pointX(index, points.length) - barWidth / 2;
        const height = Math.max(2, (point.value / maximum) * 160);
        const y = 205 - height;
        return <rect fill={color} height={height} key={`${item.name}:${point.label}`} width={barWidth} x={x} y={y}>{tooltipEnabled ? <title>{`${item.name}: ${point.label} ${point.value}`}</title> : null}</rect>;
      })}</g>;
    }
    const polyline = points.map((point, index) => `${pointX(index, points.length)},${205 - Math.max(0, (point.value / maximum) * 160)}`).join(' ');
    return <g key={item.name}>{chartType === 'area' ? <polygon fill={`${color}33`} points={`48,205 ${polyline} ${48 + width},205`} /> : null}<polyline fill="none" points={polyline} stroke={color} strokeWidth="3" />{points.map((point, index) => <circle cx={pointX(index, points.length)} cy={205 - Math.max(0, (point.value / maximum) * 160)} fill={color} key={`${item.name}:${point.label}`} r="4">{tooltipEnabled ? <title>{`${item.name}: ${point.label} ${point.value}`}</title> : null}</circle>)}</g>;
  });
}

function readSeries(seriesValue: unknown, dataValue: unknown, axis: ChartAxis): ChartSeries[] {
  if (Array.isArray(seriesValue) && seriesValue.length > 0) {
    const result = seriesValue.flatMap((item, index) => {
      if (!item || typeof item !== 'object' || Array.isArray(item)) return [];
      const record = item as Record<string, unknown>;
      return [{ name: String(record.name ?? `Series ${index + 1}`), data: readPoints(record.data, axis) }];
    });
    if (result.length > 0) return result;
  }
  return [{ name: 'Series 1', data: readPoints(dataValue, axis) }];
}

function readPoints(value: unknown, axis: ChartAxis): ChartPoint[] {
  if (!Array.isArray(value)) return [];
  return value.flatMap((item, index) => {
    if (!item || typeof item !== 'object') return [];
    const record = item as Record<string, unknown>;
    const numeric = Number(record[axis.yKey] ?? record.value ?? record.y);
    return Number.isFinite(numeric) ? [{ label: String(record[axis.xKey] ?? record.label ?? record.x ?? index + 1), value: numeric }] : [];
  });
}

function readAxis(value: unknown): ChartAxis {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return { xKey: 'label', yKey: 'value' };
  const axis = value as Record<string, unknown>;
  return { xKey: String(axis.xKey ?? 'label'), yKey: String(axis.yKey ?? 'value'), xLabel: readOptionalString(axis.xLabel), yLabel: readOptionalString(axis.yLabel) };
}

function readChartType(value: unknown): string {
  return ['bar', 'column', 'line', 'area', 'pie'].includes(String(value)) ? String(value) : 'bar';
}

function readBooleanOption(value: unknown, property: string, fallback: boolean): boolean {
  if (typeof value === 'boolean') return value;
  if (value && typeof value === 'object' && !Array.isArray(value)) return (value as Record<string, unknown>)[property] !== false;
  return fallback;
}

function readOptionalString(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim() ? value : undefined;
}

function describeChart(series: ChartSeries[], axis: ChartAxis): string {
  return `${axis.xLabel ?? axis.xKey} ${axis.yLabel ?? axis.yKey}: ${series.flatMap((item) => item.data.map((point) => `${item.name} ${point.label} ${point.value}`)).join(', ')}`;
}
