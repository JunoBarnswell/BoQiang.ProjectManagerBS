import type { DetailedHTMLProps, HTMLAttributes } from 'react';
import type { PrintDesignerElement } from 'vue-print-designer';

declare global {
  namespace JSX {
    interface IntrinsicElements {
      'print-designer': DetailedHTMLProps<HTMLAttributes<PrintDesignerElement>, PrintDesignerElement>;
    }
  }
}

declare module 'vue-print-designer' {
  interface PrintDesignerElement {
    setAvailableVariables?: (variables: Array<{ id: string; label: string; isArray?: boolean; children?: unknown[] }>) => void;
  }
}

export {};
