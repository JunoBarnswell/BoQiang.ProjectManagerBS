export interface SseFrame {
  data: string;
  event: string;
  id?: string;
  retry?: number;
}

export async function readSseStream(response: Response, onFrame: (frame: SseFrame) => void): Promise<void> {
  if (!response.body) {
    throw new Error('SSE response has no readable body.');
  }

  const decoder = new TextDecoder('utf-8');
  const reader = response.body.getReader();
  let buffer = '';

  while (true) {
    const { done, value } = await reader.read();
    if (done) {
      break;
    }

    buffer += decoder.decode(value, { stream: true });
    buffer = dispatchBufferedFrames(buffer, onFrame);
  }

  buffer += decoder.decode();
  dispatchBufferedFrames(buffer, onFrame, true);
}

export function parseJsonSseFrame<TEvent>(frame: SseFrame, fallbackEvent?: string): TEvent | null {
  try {
    const parsed = JSON.parse(frame.data) as TEvent;
    if (
      fallbackEvent &&
      parsed &&
      typeof parsed === 'object' &&
      !('event' in parsed)
    ) {
      return { ...parsed, event: fallbackEvent } as TEvent;
    }

    return parsed;
  } catch {
    return null;
  }
}

function dispatchBufferedFrames(buffer: string, onFrame: (frame: SseFrame) => void, flush = false): string {
  const normalized = buffer.replace(/\r\n/g, '\n');
  let remaining = normalized;
  let boundaryIndex = remaining.indexOf('\n\n');

  while (boundaryIndex >= 0) {
    const frame = parseFrame(remaining.slice(0, boundaryIndex));
    remaining = remaining.slice(boundaryIndex + 2);
    if (frame) {
      onFrame(frame);
    }

    boundaryIndex = remaining.indexOf('\n\n');
  }

  if (flush && remaining.trim()) {
    const frame = parseFrame(remaining);
    if (frame) {
      onFrame(frame);
    }

    return '';
  }

  return remaining;
}

function parseFrame(rawFrame: string): SseFrame | null {
  const dataLines: string[] = [];
  let event = '';
  let id: string | undefined;
  let retry: number | undefined;

  rawFrame.split('\n').forEach((line) => {
    if (!line || line.startsWith(':')) {
      return;
    }

    if (line.startsWith('event:')) {
      event = line.slice(6).trim();
      return;
    }

    if (line.startsWith('data:')) {
      dataLines.push(line.slice(5).trimStart());
      return;
    }

    if (line.startsWith('id:')) {
      id = line.slice(3).trim();
      return;
    }

    if (line.startsWith('retry:')) {
      const parsedRetry = Number(line.slice(6).trim());
      retry = Number.isFinite(parsedRetry) ? parsedRetry : undefined;
    }
  });

  if (dataLines.length === 0) {
    return null;
  }

  return {
    data: dataLines.join('\n'),
    event,
    id,
    retry
  };
}
