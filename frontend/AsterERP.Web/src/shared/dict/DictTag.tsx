import { useDict } from './useDict';

interface DictTagProps {
  dictType: string;
  value: string;
}

export function DictTag({ dictType, value }: DictTagProps) {
  const { options } = useDict(dictType);
  const option = options.find((item) => item.value === value);

  if (!option) {
    return <span className="dict-tag">{value}</span>;
  }

  return (
    <span
      className="dict-tag"
      style={{
        backgroundColor: option.color ? `${option.color}1f` : 'var(--app-accent-soft)',
        borderColor: option.color ? `${option.color}44` : 'var(--app-border)',
        color: option.color ?? 'var(--app-text)'
      }}
    >
      {option.label}
    </span>
  );
}
