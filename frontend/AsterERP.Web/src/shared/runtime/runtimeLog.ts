export type RuntimeLogLevel = 'debug' | 'error' | 'info' | 'off' | 'warn';

export interface RuntimeLogEntry {
  details?: Record<string, unknown>;
  event: string;
  level: RuntimeLogLevel;
  timestamp: string;
}

const logLevelRank: Record<RuntimeLogLevel, number> = {
  debug: 10,
  info: 20,
  warn: 30,
  error: 40,
  off: 50
};

const allowedLogLevels = new Set<RuntimeLogLevel>(['debug', 'info', 'warn', 'error', 'off']);

export const runtimeLog = {
  debug: (event: string, details?: Record<string, unknown>) => writeRuntimeLog('debug', event, details),
  error: (event: string, details?: Record<string, unknown>) => writeRuntimeLog('error', event, details),
  info: (event: string, details?: Record<string, unknown>) => writeRuntimeLog('info', event, details),
  warn: (event: string, details?: Record<string, unknown>) => writeRuntimeLog('warn', event, details)
};

function writeRuntimeLog(level: Exclude<RuntimeLogLevel, 'off'>, event: string, details?: Record<string, unknown>) {
  const configuredLevel = readRuntimeLogLevel();
  if (logLevelRank[level] < logLevelRank[configuredLevel]) {
    return;
  }

  const entry: RuntimeLogEntry = {
    details,
    event,
    level,
    timestamp: new Date().toISOString()
  };

  emitRuntimeLogEvent(entry);
  writeConsoleLog(entry);
}

function readRuntimeLogLevel(): RuntimeLogLevel {
  if (typeof window !== 'undefined') {
    const configured = window.localStorage.getItem('astererp.runtimeLogLevel')?.trim().toLowerCase() as RuntimeLogLevel | undefined;
    if (configured && allowedLogLevels.has(configured)) {
      return configured;
    }
  }

  return import.meta.env.DEV ? 'debug' : 'warn';
}

function emitRuntimeLogEvent(entry: RuntimeLogEntry) {
  if (typeof window === 'undefined') {
    return;
  }

  window.dispatchEvent(new CustomEvent('astererp:runtime-log', { detail: entry }));
}

function writeConsoleLog(entry: RuntimeLogEntry) {
  const message = `[AsterERP Runtime] ${entry.event}`;
  const runtimeConsole = globalThis.console;
  switch (entry.level) {
    case 'debug':
      runtimeConsole.debug(message, entry.details ?? {});
      break;
    case 'info':
      runtimeConsole.info(message, entry.details ?? {});
      break;
    case 'warn':
      runtimeConsole.warn(message, entry.details ?? {});
      break;
    case 'error':
      runtimeConsole.error(message, entry.details ?? {});
      break;
    default:
      break;
  }
}
