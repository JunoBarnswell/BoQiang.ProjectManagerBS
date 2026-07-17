import { useEffect, useState, type RefObject } from 'react';

export interface ElementSize {
  height: number;
  width: number;
}

export function useElementSize<TElement extends HTMLElement>(targetRef: RefObject<TElement | null>) {
  const [size, setSize] = useState<ElementSize>({
    height: 0,
    width: 0
  });

  useEffect(() => {
    const element = targetRef.current;

    if (!element) {
      return;
    }

    const measure = () => {
      const rect = element.getBoundingClientRect();
      setSize({
        height: Math.round(rect.height),
        width: Math.round(rect.width)
      });
    };

    measure();

    if (typeof ResizeObserver === 'undefined') {
      window.addEventListener('resize', measure, { passive: true });
      return () => window.removeEventListener('resize', measure);
    }

    const observer = new ResizeObserver(() => {
      measure();
    });

    observer.observe(element);

    return () => observer.disconnect();
  }, [targetRef]);

  return size;
}

