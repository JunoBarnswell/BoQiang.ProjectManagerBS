import type { ProjectManagementHomeFilterGroup, ProjectManagementHomeFilterRule, ProjectManagementMemberCandidate } from '../../../api/project-management/projectManagement.types';
import { PmBox, PmButton, PmFormInput, PmFormSelect, PmIcon, PmMenuItem, PmText } from '../../../ui/project-management';

interface ProjectHomeFilterBuilderProps {
  filter: ProjectManagementHomeFilterGroup;
  onChange: (filter: ProjectManagementHomeFilterGroup) => void;
  translate: (key: string) => string;
  statusLabels: Record<string, string>;
  priorityLabels: Record<string, string>;
  healthLabels: Record<string, string>;
  memberOptions?: ProjectManagementMemberCandidate[];
}

const fields = ['health', 'priority', 'lead', 'members', 'status', 'startDate', 'targetDate', 'labels', 'issuesCount', 'updated', 'created', 'projectKey', 'workspace', 'archived'] as const;
const operators = ['is', 'isNot', 'contains', 'notContains', 'in', 'notIn', 'before', 'beforeOrOn', 'after', 'afterOrOn', 'equals', 'notEquals', 'greaterThan', 'greaterOrEqual', 'lessThan', 'lessOrEqual', 'between', 'today', 'thisWeek', 'overdue', 'isEmpty', 'isNotEmpty'] as const;
export function ProjectHomeFilterBuilder({ filter, onChange, translate, statusLabels, priorityLabels, healthLabels, memberOptions = [] }: ProjectHomeFilterBuilderProps) {
  const updateRule = (index: number, next: Partial<ProjectManagementHomeFilterRule>) => {
    const rules = filter.rules.map((rule, ruleIndex) => ruleIndex === index ? { ...rule, ...next } : rule);
    onChange({ conjunction: 'and', rules });
  };
  const removeRule = (index: number) => onChange({ conjunction: 'and', rules: filter.rules.filter((_, ruleIndex) => ruleIndex !== index) });
  const addRule = () => onChange({ conjunction: 'and', rules: [...filter.rules, { field: 'status', operator: 'is', values: ['Planning'] }] });

  return <PmBox sx={{ display: 'grid', gap: 1.25, minWidth: 340 }}>
    <PmText fontSize=".76rem" fontWeight={700}>{translate('projectManagement.home.filterRules')}</PmText>
    {filter.rules.length === 0 && <PmText color="text.secondary" fontSize=".76rem">{translate('projectManagement.home.noActiveFilters')}</PmText>}
    {filter.rules.map((rule, index) => <PmBox key={`${rule.field}-${index}`} sx={{ display: 'grid', gridTemplateColumns: 'minmax(110px, .9fr) minmax(100px, .8fr) minmax(120px, 1fr) 32px', gap: .75, alignItems: 'center' }}>
      <PmFormSelect label={translate('projectManagement.home.filterField')} value={rule.field} onChange={event => updateRule(index, { field: String(event.target.value), values: [] })}>
        {fields.map(field => <PmMenuItem key={field} value={field}>{translate(`projectManagement.home.filterField.${field}`)}</PmMenuItem>)}
      </PmFormSelect>
      <PmFormSelect label={translate('projectManagement.home.filterOperator')} value={rule.operator} onChange={event => {
        const operator = String(event.target.value);
        updateRule(index, { operator, values: ['isEmpty', 'isNotEmpty'].includes(operator) ? ['__empty__'] : ['today', 'thisWeek', 'overdue'].includes(operator) ? ['__relative__'] : rule.values });
      }}>
        {operators.map(operator => <PmMenuItem key={operator} value={operator}>{translate(`projectManagement.home.filterOperator.${operator}`)}</PmMenuItem>)}
      </PmFormSelect>
      <FilterValueInput field={rule.field} values={rule.values} healthLabels={healthLabels} priorityLabels={priorityLabels} statusLabels={statusLabels} memberOptions={memberOptions} translate={translate} onChange={values => updateRule(index, { values })} />
      <PmButton aria-label={translate('projectManagement.home.removeFilterRule')} onClick={() => removeRule(index)} size="small"><PmIcon name="trash" size={15} /></PmButton>
    </PmBox>)}
    <PmButton onClick={addRule} startIcon={<PmIcon name="plus" size={15} />} variant="outlined">{translate('projectManagement.home.addFilterRule')}</PmButton>
  </PmBox>;
}

function FilterValueInput({ field, values, healthLabels, priorityLabels, statusLabels, memberOptions, translate, onChange }: { field: string; values: string[]; healthLabels: Record<string, string>; priorityLabels: Record<string, string>; statusLabels: Record<string, string>; memberOptions: ProjectManagementMemberCandidate[]; translate: (key: string) => string; onChange: (values: string[]) => void }) {
  if (field === 'health' || field === 'priority' || field === 'status' || field === 'archived') {
    const labels = field === 'health' ? healthLabels : field === 'priority' ? priorityLabels : statusLabels;
    const options = field === 'archived' ? ['true', 'false'] : Object.keys(labels);
    return <PmFormSelect label={translate('projectManagement.home.filterValue')} value={values[0] ?? ''} onChange={event => onChange([String(event.target.value)])}>
      {options.map(value => <PmMenuItem key={value} value={value}>{field === 'archived' ? translate(value === 'true' ? 'projectManagement.home.archivedOnly' : 'projectManagement.home.activeOnly') : translate(labels[value])}</PmMenuItem>)}
    </PmFormSelect>;
  }
  if ((field === 'lead' || field === 'members') && memberOptions.length > 0) {
    return <PmFormSelect label={translate('projectManagement.home.filterValue')} value={values[0] ?? ''} onChange={event => onChange([String(event.target.value)])}>
      {memberOptions.filter(option => option.isSelectable).map(option => <PmMenuItem key={`${option.userId}-${option.employmentId}`} value={option.userId}>{option.displayName || option.userName}</PmMenuItem>)}
    </PmFormSelect>;
  }
  return <PmFormInput aria-label={translate('projectManagement.home.filterValue')} onChange={event => onChange(event.target.value.split(',').map(value => value.trim()).filter(Boolean))} placeholder={translate('projectManagement.home.filterValueHint')} size="small" value={values.join(', ')} />;
}
