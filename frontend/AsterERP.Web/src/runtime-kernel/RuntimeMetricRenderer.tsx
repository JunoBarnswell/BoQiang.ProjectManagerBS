import type { ReactNode } from 'react';

import type { RuntimeComponentRenderContext } from './RuntimeComponentTypes';
import { applyRuntimeNodePresentation } from './RuntimeNodePresentation';

export function hasMetricRuntimeRenderer(type: string): boolean { return type === 'metric.progress' || type === 'metric.meter' || type === 'output.value'; }

export function renderMetricRuntime(context: RuntimeComponentRenderContext): ReactNode {
  if (context.componentType === 'output.value') {
    const value = context.value === null || context.value === undefined || context.value === '' ? context.props.fallback : context.value;
    return applyRuntimeNodePresentation(context, <output className="text-sm text-slate-700">{String(value ?? '')}</output>);
  }
  if (context.componentType === 'metric.meter') {
    const min = numberProp(context.props.min, 0);
    const max = numberProp(context.props.max, 100);
    const value = clamp(numberProp(context.value, min), min, max);
    return applyRuntimeNodePresentation(context, <div className="flex w-full items-center gap-2"><meter className="h-4 min-w-0 flex-1" high={numberProp(context.props.high, 80)} low={numberProp(context.props.low, 20)} max={max} min={min} optimum={numberProp(context.props.optimum, 60)} value={value} /><span className="w-12 text-right text-xs tabular-nums text-slate-500">{value}</span></div>);
  }
  const max = numberProp(context.props.max, 100);
  const value = numberProp(context.value, 0);
  const clamped = clamp(value, 0, max);
  return applyRuntimeNodePresentation(context, <div className="flex w-full items-center gap-2"><progress className="h-3 min-w-0 flex-1" max={max} value={context.props.indeterminate ? undefined : clamped} />{!context.props.indeterminate ? <span className="w-12 text-right text-xs tabular-nums text-slate-500">{clamped}</span> : null}</div>);
}

function numberProp(value: unknown, fallback: number): number { const parsed = Number(value); return Number.isFinite(parsed) ? parsed : fallback; }
function clamp(value: number, min: number, max: number): number { return Math.max(min, Math.min(max, value)); }
