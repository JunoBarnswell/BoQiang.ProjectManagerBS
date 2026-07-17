interface BreadcrumbViewProps {
  items: string[];
}

export function BreadcrumbView({ items }: BreadcrumbViewProps) {
  return (
    <nav className="breadcrumb" aria-label="Breadcrumbs">
      {items.map((item, index) => (
        <span key={item} className="breadcrumb-item">
          {item}
          {index < items.length - 1 ? <span className="breadcrumb-separator">/</span> : null}
        </span>
      ))}
    </nav>
  );
}
