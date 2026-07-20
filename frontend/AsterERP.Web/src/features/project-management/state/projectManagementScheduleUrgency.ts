export type ScheduleUrgencyTone = 'completed' | 'critical' | 'healthy' | 'none' | 'overdue' | 'warning';

export interface ScheduleUrgencyMetrics {
  progressPercent: number;
  remainingDays: number;
  remainingRatio: number;
  tone: ScheduleUrgencyTone;
  urgencyColor: string;
}

const defaultWindowMs = 14 * 86_400_000;
const completedStatuses = new Set(['Done', 'Cancelled', 'Completed', 'Archived']);

export function computeScheduleUrgencyMetrics(
  startDate?: string,
  dueDate?: string,
  status?: string,
  now = Date.now(),
): ScheduleUrgencyMetrics {
  if (!dueDate) {
    return {
      progressPercent: 0,
      remainingDays: 0,
      remainingRatio: 1,
      tone: 'none',
      urgencyColor: '#94a3b8',
    };
  }

  if (status && completedStatuses.has(status)) {
    return {
      progressPercent: 100,
      remainingDays: 0,
      remainingRatio: 0,
      tone: 'completed',
      urgencyColor: '#22c55e',
    };
  }

  const end = parseScheduleDate(dueDate);
  if (end === undefined) {
    return {
      progressPercent: 0,
      remainingDays: 0,
      remainingRatio: 1,
      tone: 'none',
      urgencyColor: '#94a3b8',
    };
  }

  let start = parseScheduleDate(startDate);
  if (start === undefined || start >= end) {
    start = end - defaultWindowMs;
  }

  const total = Math.max(end - start, 1);
  const remainingMs = end - now;
  const remainingRatio = clamp(remainingMs / total, 0, 1);
  const progressPercent = clamp(Math.round((1 - remainingRatio) * 100), 0, 100);
  const remainingDays = remainingMs <= 0 ? 0 : Math.ceil(remainingMs / 86_400_000);

  if (remainingMs < 0) {
    return {
      progressPercent: 100,
      remainingDays: Math.max(1, Math.ceil(-remainingMs / 86_400_000)),
      remainingRatio: 0,
      tone: 'overdue',
      urgencyColor: '#dc2626',
    };
  }

  const tone = resolveTone(remainingMs);
  return {
    progressPercent,
    remainingDays,
    remainingRatio,
    tone,
    urgencyColor: resolveUrgencyColor(remainingRatio, tone),
  };
}

function resolveTone(remainingMs: number): ScheduleUrgencyTone {
  if (remainingMs < 86_400_000) return 'critical';
  if (remainingMs < 3 * 86_400_000) return 'warning';
  return 'healthy';
}

function resolveUrgencyColor(remainingRatio: number, tone: ScheduleUrgencyTone): string {
  if (tone === 'none') return '#94a3b8';
  if (tone === 'completed') return '#22c55e';
  if (tone === 'overdue') return '#dc2626';

  const ratio = clamp(remainingRatio, 0, 1);
  if (ratio >= 0.5) {
    return mixHexColor('#22c55e', '#eab308', (1 - ratio) / 0.5);
  }

  return mixHexColor('#eab308', '#ef4444', (0.5 - ratio) / 0.5);
}

function mixHexColor(from: string, to: string, amount: number): string {
  const start = hexToRgb(from);
  const end = hexToRgb(to);
  const ratio = clamp(amount, 0, 1);
  const red = Math.round(start.r + (end.r - start.r) * ratio);
  const green = Math.round(start.g + (end.g - start.g) * ratio);
  const blue = Math.round(start.b + (end.b - start.b) * ratio);
  return `rgb(${red}, ${green}, ${blue})`;
}

function hexToRgb(value: string): { r: number; g: number; b: number } {
  const normalized = value.replace('#', '');
  return {
    r: Number.parseInt(normalized.slice(0, 2), 16),
    g: Number.parseInt(normalized.slice(2, 4), 16),
    b: Number.parseInt(normalized.slice(4, 6), 16),
  };
}

function parseScheduleDate(value?: string): number | undefined {
  if (!value) return undefined;
  const parsed = new Date(value.length <= 10 ? `${value}T23:59:59` : value).getTime();
  return Number.isNaN(parsed) ? undefined : parsed;
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}
