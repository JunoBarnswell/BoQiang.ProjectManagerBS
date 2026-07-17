let loadPromise: Promise<void> | null = null;

export function loadPrintDesignerElement(): Promise<void> {
  if (!loadPromise) {
    loadPromise = Promise.all([
      import('vue-print-designer'),
      import('vue-print-designer/style.css')
    ]).then(() => undefined);
  }

  return loadPromise;
}
