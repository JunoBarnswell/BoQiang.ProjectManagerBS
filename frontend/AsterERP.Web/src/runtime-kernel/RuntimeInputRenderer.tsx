import { useEffect, useRef, useState, type PointerEvent as ReactPointerEvent, type ReactNode } from 'react';

import type { RuntimeComponentRenderContext } from './RuntimeComponentTypes';
import { applyRuntimeNodePresentation } from './RuntimeNodePresentation';

export function hasInputRuntimeRenderer(type: string): boolean {
  return new Set([
    'input.color', 'input.date', 'input.datetimeLocal', 'input.email', 'input.file', 'input.hidden', 'input.month',
    'input.number', 'input.password', 'input.range', 'input.search', 'input.tel', 'input.text', 'input.textarea', 'input.time', 'input.url', 'input.week',
    'media.fileUpload', 'media.imageUpload', 'media.signature'
  ]).has(type);
}

export function renderInputRuntime(context: RuntimeComponentRenderContext): ReactNode {
  if (context.componentType === 'input.textarea') {
    return applyRuntimeNodePresentation(context, <textarea className="form-input min-h-24 w-full" disabled={context.disabled} placeholder={String(context.props.placeholder ?? context.title)} readOnly={context.readOnly} rows={optionalNumber(context.props.rows) ?? 4} style={{ resize: textareaResize(context.props.resize) }} value={inputValue(context.value)} onChange={(event) => context.onChange(event.target.value, context.changeAction)} />);
  }
  if (context.componentType === 'media.signature') return <SignatureRuntime context={context} />;
  if (context.componentType === 'media.fileUpload' || context.componentType === 'media.imageUpload') {
    return applyRuntimeNodePresentation(context, <input accept={context.componentType === 'media.imageUpload' ? 'image/*' : typeof context.props.accept === 'string' ? context.props.accept : undefined} className="form-input h-9 w-full" disabled={context.disabled || context.readOnly} multiple={context.props.multiple === true} type="file" onChange={(event) => context.onChange(context.props.multiple === true ? [...(event.target.files ?? [])] : event.target.files?.[0] ?? null, context.changeAction)} />);
  }
  if (context.componentType === 'input.email') {
    return applyRuntimeNodePresentation(context, <input autoComplete={String(context.props.autocomplete ?? 'email')} className="form-input h-9 w-full" disabled={context.disabled} pattern={typeof context.props.pattern === 'string' ? context.props.pattern : undefined} placeholder={String(context.props.placeholder ?? context.title)} readOnly={context.readOnly} type="email" value={inputValue(context.value)} onChange={(event) => context.onChange(event.target.value, context.changeAction)} />);
  }
  if (context.componentType === 'input.date') {
    return applyRuntimeNodePresentation(context, <input aria-label={context.title} autoComplete="off" className="form-input h-9 w-full" disabled={context.disabled} inputMode="numeric" maxLength={10} pattern="\\d{4}-\\d{2}-\\d{2}" placeholder={String(context.props.placeholder ?? 'YYYY-MM-DD')} readOnly={context.readOnly} type="text" value={inputValue(context.value)} onChange={(event) => context.onChange(event.target.value, context.changeAction)} />);
  }
  if (context.componentType === 'input.range') return <RangeRuntime context={context} />;
  const inputType = toInputType(typeof context.props.inputType === 'string' ? context.props.inputType : context.componentType === 'input.text' ? 'text' : context.componentType.slice('input.'.length));
  if (inputType === 'hidden') return applyRuntimeNodePresentation(context, <input type="hidden" value={inputValue(context.value)} onChange={(event) => context.onChange(event.target.value, context.changeAction)} />);
  if (inputType === 'file') return applyRuntimeNodePresentation(context, <input accept={typeof context.props.accept === 'string' ? context.props.accept : undefined} className="form-input h-9 w-full" disabled={context.disabled || context.readOnly} multiple={context.props.multiple === true} type="file" onChange={(event) => context.onChange(context.props.multiple === true ? [...(event.target.files ?? [])] : event.target.files?.[0] ?? null, context.changeAction)} />);
  return applyRuntimeNodePresentation(context, <input autoComplete={typeof context.props.autoComplete === 'string' ? context.props.autoComplete : undefined} className="form-input h-9 w-full" disabled={context.disabled} max={inputType === 'number' ? optionalNumber(context.props.max) : undefined} maxLength={optionalNumber(context.props.maxLength)} min={inputType === 'number' ? optionalNumber(context.props.min) : undefined} placeholder={String(context.props.placeholder ?? context.title)} readOnly={context.readOnly} step={inputType === 'number' ? optionalNumber(context.props.step) : undefined} type={inputType} value={inputValue(context.value)} onChange={(event) => context.onChange(event.target.value, context.changeAction)} />);
}

function SignatureRuntime({ context }: { context: RuntimeComponentRenderContext }): ReactNode {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const drawingRef = useRef(false);
  const strokesRef = useRef<SignatureStroke[]>(readStrokes(context.value));
  const [strokes, setStrokes] = useState<SignatureStroke[]>(strokesRef.current);
  const resourceId = readResourceId(context);
  const penColor = typeof context.props.penColor === 'string' ? context.props.penColor : '#111827';

  useEffect(() => {
    const nextStrokes = readStrokes(context.value);
    strokesRef.current = nextStrokes;
    setStrokes(nextStrokes);
  }, [context.value]);
  useEffect(() => {
    drawSignature(canvasRef.current, strokes, penColor);
  }, [penColor, strokes]);

  const commit = (nextStrokes: SignatureStroke[]) => {
    strokesRef.current = nextStrokes;
    setStrokes(nextStrokes);
    context.onChange(signatureResource(resourceId, nextStrokes), context.changeAction);
  };
  const pointFor = (event: ReactPointerEvent<HTMLCanvasElement>): SignaturePoint => {
    const bounds = event.currentTarget.getBoundingClientRect();
    return { x: Math.max(0, Math.min(640, (event.clientX - bounds.left) * (640 / bounds.width))), y: Math.max(0, Math.min(240, (event.clientY - bounds.top) * (240 / bounds.height))) };
  };
  const startDrawing = (event: ReactPointerEvent<HTMLCanvasElement>) => {
    if (context.disabled || context.readOnly) return;
    drawingRef.current = true;
    event.currentTarget.setPointerCapture(event.pointerId);
    strokesRef.current = [...strokesRef.current, [pointFor(event)]];
    setStrokes(strokesRef.current);
  };
  const continueDrawing = (event: ReactPointerEvent<HTMLCanvasElement>) => {
    if (!drawingRef.current) return;
    const current = strokesRef.current.at(-1);
    if (!current) return;
    strokesRef.current = [...strokesRef.current.slice(0, -1), [...current, pointFor(event)]];
    setStrokes(strokesRef.current);
  };
  const finishDrawing = (event: ReactPointerEvent<HTMLCanvasElement>) => {
    if (!drawingRef.current) return;
    drawingRef.current = false;
    if (event.currentTarget.hasPointerCapture(event.pointerId)) event.currentTarget.releasePointerCapture(event.pointerId);
    commit(strokesRef.current);
  };
  const undo = () => commit(strokesRef.current.slice(0, -1));
  const clear = () => commit([]);
  return applyRuntimeNodePresentation(context, <div className="grid gap-2">
    <canvas aria-label={context.title} className="h-32 w-full rounded border border-slate-300 bg-white" height={240} ref={canvasRef} width={640} onPointerCancel={finishDrawing} onPointerDown={startDrawing} onPointerMove={continueDrawing} onPointerUp={finishDrawing} />
    <div className="flex gap-2">
      <button aria-label="Undo signature stroke" className="secondary-button h-8 justify-self-start" disabled={context.disabled || context.readOnly || strokes.length === 0} type="button" onClick={undo}>Undo</button>
      <button className="secondary-button h-8 justify-self-start" disabled={context.disabled || context.readOnly} type="button" onClick={clear}>Clear signature</button>
    </div>
  </div>);
}

interface SignaturePoint { x: number; y: number; }
type SignatureStroke = SignaturePoint[];

function readResourceId(context: RuntimeComponentRenderContext): string {
  const value = context.value && typeof context.value === 'object' ? context.value as Record<string, unknown> : {};
  return String(value.resourceId ?? context.props.resourceId ?? `signature:${context.element.id}`);
}

function readStrokes(value: unknown): SignatureStroke[] {
  if (!value || typeof value !== 'object' || !Array.isArray((value as Record<string, unknown>).strokes)) return [];
  return ((value as Record<string, unknown>).strokes as unknown[]).flatMap((stroke) => Array.isArray(stroke) ? [stroke.flatMap((point) => point && typeof point === 'object' && Number.isFinite(Number((point as Record<string, unknown>).x)) && Number.isFinite(Number((point as Record<string, unknown>).y)) ? [{ x: Number((point as Record<string, unknown>).x), y: Number((point as Record<string, unknown>).y) }] : [])] : []);
}

function signatureResource(resourceId: string, strokes: SignatureStroke[]): Record<string, unknown> {
  return { conversionPipeline: [], displayName: 'Signature', expectedType: 'json', resourceId, resourceType: 'signature', strokes, valueType: 'json' };
}

function drawSignature(canvas: HTMLCanvasElement | null, strokes: SignatureStroke[], color: string): void {
  if (!canvas) return;
  const context = canvas.getContext('2d');
  if (!context) return;
  context.clearRect(0, 0, canvas.width, canvas.height);
  context.strokeStyle = color;
  context.lineWidth = 2;
  context.lineCap = 'round';
  strokes.forEach((stroke) => {
    if (stroke.length === 0) return;
    context.beginPath();
    context.moveTo(stroke[0].x, stroke[0].y);
    stroke.slice(1).forEach((point) => context.lineTo(point.x, point.y));
    context.stroke();
  });
}

function RangeRuntime({ context }: { context: RuntimeComponentRenderContext }) {
  const min = numberProp(context.props.min, 0);
  const max = numberProp(context.props.max, 100);
  const step = numberProp(context.props.step, 1);
  const value = clamp(Number(context.value ?? min), min, max);
  return applyRuntimeNodePresentation(context, <div className="flex w-full items-center gap-2 text-xs text-slate-600"><input className="min-w-0 flex-1" disabled={context.disabled || context.readOnly} max={max} min={min} step={step} type="range" value={Number.isFinite(value) ? value : min} onChange={(event) => context.onChange(Number(event.target.value), context.changeAction)} />{context.props.showValue !== false ? <output className="w-12 text-right tabular-nums">{Number.isFinite(value) ? value : min}</output> : null}</div>);
}

function toInputType(value: string): string { return value === 'datetimeLocal' ? 'datetime-local' : value.trim() || 'text'; }
function inputValue(value: unknown): string { return Array.isArray(value) ? String(value[0] ?? '') : String(value ?? ''); }
function optionalNumber(value: unknown): number | undefined { const parsed = Number(value); return Number.isFinite(parsed) && parsed > 0 ? parsed : undefined; }
function textareaResize(value: unknown): 'none' | 'vertical' | 'both' { return value === 'none' || value === 'both' ? value : 'vertical'; }
function numberProp(value: unknown, fallback: number): number { const parsed = Number(value); return Number.isFinite(parsed) ? parsed : fallback; }
function clamp(value: number, min: number, max: number): number { return Number.isFinite(value) ? Math.max(min, Math.min(max, value)) : min; }
