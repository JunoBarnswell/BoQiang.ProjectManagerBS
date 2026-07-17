import { AdaptiveSearchForm } from '../responsive/AdaptiveSearchForm';

import type { FormFieldConfig } from './formTypes';

interface SearchFormProps<TValues extends object> {
  fields: FormFieldConfig<TValues>[];
  loading?: boolean;
  onReset?: () => void;
  onValueChange: (value: TValues) => void;
  onSubmit: (value: TValues) => void;
  value: TValues;
}

export function SearchForm<TValues extends object>(props: SearchFormProps<TValues>) {
  return <AdaptiveSearchForm {...props} defaultCollapsed={false} maxRows={2} />;
}
