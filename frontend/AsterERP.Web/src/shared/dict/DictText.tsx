import { useDict } from './useDict';

interface DictTextProps {
  dictType: string;
  value: string;
}

export function DictText({ dictType, value }: DictTextProps) {
  const { options } = useDict(dictType);
  const option = options.find((item) => item.value === value);

  return <>{option?.label ?? value}</>;
}
