import { useEffect, useMemo, useSyncExternalStore } from 'react';

import { getDictOptions as getRemoteDictOptions } from '../../api/system/dicts.api';
import { useI18n } from '../../core/i18n/I18nProvider';
import { queryKeys } from '../../core/query/queryKeys';
import { useApiQuery } from '../../core/query/useApiQuery';

import { getDictOptions, getDictSnapshot, resolveDictOptionLabel, setDictOptions, subscribeDictStore } from './dictStore';
import type { DictOption } from './dictTypes';

const localOnlyDictTypes = new Set(['locale', 'theme_mode']);

function normalizeDictType(dictType: string): string {
  return dictType.trim();
}

function mapOptions(options: Array<{ color?: string | null; disabled?: boolean; label: string; value: string }>): DictOption[] {
  return options.map((option) => ({
    color: option.color ?? undefined,
    disabled: option.disabled,
    label: option.label,
    value: option.value
  }));
}

export function useDict(dictType: string) {
  const { translate } = useI18n();
  const normalizedDictType = normalizeDictType(dictType);
  const revision = useSyncExternalStore(
    subscribeDictStore,
    () => getDictSnapshot(normalizedDictType).revision,
    () => getDictSnapshot(normalizedDictType).revision
  );
  const shouldQueryRemote = normalizedDictType.length > 0 && !localOnlyDictTypes.has(normalizedDictType);
  const query = useApiQuery({
    enabled: shouldQueryRemote,
    queryFn: ({ signal }) => getRemoteDictOptions(normalizedDictType, signal),
    queryKey: queryKeys.dict.byType(normalizedDictType),
    select: (response) => mapOptions(response.data)
  });

  useEffect(() => {
    if (normalizedDictType && query.data) {
      setDictOptions(normalizedDictType, query.data);
    }
  }, [normalizedDictType, query.data]);

  const options = useMemo(
    () => {
      const sourceOptions = query.data ?? getDictOptions(normalizedDictType);
      return sourceOptions.map((option) => ({
        ...option,
        label: resolveDictOptionLabel(option.label, translate)
      }));
    },
    [normalizedDictType, query.data, translate]
  );

  return {
    isFetching: query.isFetching,
    isLoading: query.isLoading,
    options,
    revision
  };
}

export function useDictLabel(dictType: string, value: string) {
  return resolveDictOptionLabel(getDictOptions(normalizeDictType(dictType)).find((item) => item.value === value)?.label ?? value);
}
