import type { ChangeEvent } from 'react';

import { useDict } from './useDict';

interface DictRadioProps {
  dictType: string;
  name: string;
  value?: string;
  onValueChange?: (value: string) => void;
}

export function DictRadio({ dictType, name, onValueChange, value }: DictRadioProps) {
  const { options } = useDict(dictType);

  return (
    <div className="flex items-center gap-6 mt-1">
      {options.map((option) => {
        const isDanger = option.value === '0' || option.value === 'Disabled'; // Example check for danger state
        return (
          <label key={option.value} className="flex items-center gap-2 cursor-pointer">
            <input
              checked={value === option.value}
              name={name}
              onChange={(event: ChangeEvent<HTMLInputElement>) => {
                if (event.target.checked) {
                  onValueChange?.(option.value);
                }
              }}
              type="radio"
              value={option.value}
              className={`${
                isDanger 
                  ? 'text-red-600 focus:ring-red-500' 
                  : 'text-primary-600 focus:ring-primary-500'
              } w-4 h-4 cursor-pointer`}
            />
            <span className="text-gray-700">{option.label}</span>
          </label>
        );
      })}
    </div>
  );
}
