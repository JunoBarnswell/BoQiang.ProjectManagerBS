type ObservedSize = {
  height: number;
  width: number;
};

export function ensureFlowCanvasResizeObserver(): void {
  if (typeof window === 'undefined' || typeof window.ResizeObserver !== 'undefined') {
    return;
  }

  class FlowCanvasResizeObserver implements ResizeObserver {
    private readonly callback: ResizeObserverCallback;
    private readonly observed = new Map<Element, ObservedSize>();
    private frameId: number | null = null;

    constructor(callback: ResizeObserverCallback) {
      this.callback = callback;
    }

    observe(target: Element): void {
      const rect = target.getBoundingClientRect();
      this.observed.set(target, { height: rect.height, width: rect.width });
      this.schedule();
    }

    unobserve(target: Element): void {
      this.observed.delete(target);
      if (this.observed.size === 0) {
        this.cancel();
      }
    }

    disconnect(): void {
      this.observed.clear();
      this.cancel();
    }

    private schedule(): void {
      if (this.frameId !== null || this.observed.size === 0) {
        return;
      }

      this.frameId = window.requestAnimationFrame(() => {
        this.frameId = null;
        this.flush();
        this.schedule();
      });
    }

    private cancel(): void {
      if (this.frameId === null) {
        return;
      }

      window.cancelAnimationFrame(this.frameId);
      this.frameId = null;
    }

    private flush(): void {
      const entries: ResizeObserverEntry[] = [];
      this.observed.forEach((previous, element) => {
        const rect = element.getBoundingClientRect();
        if (rect.width === previous.width && rect.height === previous.height) {
          return;
        }

        this.observed.set(element, { height: rect.height, width: rect.width });
        entries.push(createResizeObserverEntry(element, rect));
      });

      if (entries.length > 0) {
        this.callback(entries, this);
      }
    }
  }

  try {
    window.ResizeObserver = FlowCanvasResizeObserver;
  } catch {
    // Embedded browser sandboxes can expose a non-extensible window.
  }
}

function createResizeObserverEntry(target: Element, rect: DOMRect): ResizeObserverEntry {
  return {
    borderBoxSize: [createResizeObserverSize(rect)],
    contentBoxSize: [createResizeObserverSize(rect)],
    contentRect: rect,
    devicePixelContentBoxSize: [createResizeObserverSize(rect)],
    target
  } as ResizeObserverEntry;
}

function createResizeObserverSize(rect: DOMRect): ResizeObserverSize {
  return {
    blockSize: rect.height,
    inlineSize: rect.width
  };
}
