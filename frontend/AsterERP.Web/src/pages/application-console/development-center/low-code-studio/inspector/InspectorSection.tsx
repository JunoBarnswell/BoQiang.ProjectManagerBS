import { ChevronDown } from 'lucide-react';
import { useState, type ReactNode } from 'react';

export interface InspectorSectionProps {
  title: string;
  children: ReactNode;
  defaultOpen?: boolean;
}

export function InspectorSection({ title, children, defaultOpen = true }: InspectorSectionProps) {
  const [open, setOpen] = useState(defaultOpen);
  return <section className="page-studio__inspector-section" aria-label={title}>
    <button type="button" className="page-studio__inspector-section-toggle" aria-expanded={open} onClick={() => setOpen((value) => !value)}>
      <span>{title}</span><ChevronDown aria-hidden="true" className={`page-studio__inspector-section-chevron ${open ? 'is-open' : ''}`} />
    </button>
    <div className={`page-studio__inspector-section-content ${open ? 'is-open' : ''}`}><div>{children}</div></div>
  </section>;
}
