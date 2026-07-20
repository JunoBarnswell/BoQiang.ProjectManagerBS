import { Autocomplete, Box, Typography } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';

import { getProjectManagementMemberCandidates } from '../../../api/project-management/projectManagement.api';
import type { ProjectManagementMemberCandidate } from '../../../api/project-management/projectManagement.types';
import { projectManagementQueryKeys } from '../../../core/query/projectManagementQueryKeys';
import { PmFormInput } from '../../../ui/project-management';
import { useProjectManagementI18n } from '../projectManagementI18n';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

import { buildMemberSearchLabel, dedupeUserCandidates, formatMemberCandidate } from './memberCandidateUtils';

const SEARCH_DEBOUNCE_MS = 180;
const SEARCH_PAGE_SIZE = 20;

export type ProjectMemberCandidateAutocompleteProps = {
  disabled?: boolean;
  excludeUserIds?: ReadonlySet<string> | string[];
  label?: string;
  onCandidatesChange?: (candidates: ProjectManagementMemberCandidate[]) => void;
  onQueryStatusChange?: (status: { isError: boolean; refetch: () => void }) => void;
  onUserChange: (userId: string, candidate: ProjectManagementMemberCandidate | null) => void;
  placeholder?: string;
  value: string;
  /** When editing, show this label if the user is not in the current search results. */
  valueLabel?: string;
};

export function ProjectMemberCandidateAutocomplete({
  disabled = false,
  excludeUserIds,
  label,
  onCandidatesChange,
  onQueryStatusChange,
  onUserChange,
  placeholder,
  value,
  valueLabel,
}: ProjectMemberCandidateAutocompleteProps) {
  const { t } = useProjectManagementI18n();
  const scope = useProjectManagementWorkspaceScope();
  const [inputValue, setInputValue] = useState('');
  const [debouncedKeyword, setDebouncedKeyword] = useState('');
  const [selectedOption, setSelectedOption] = useState<ProjectManagementMemberCandidate | null>(null);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedKeyword(inputValue.trim());
    }, SEARCH_DEBOUNCE_MS);
    return () => window.clearTimeout(timer);
  }, [inputValue]);

  const excludeSet = useMemo(() => {
    if (!excludeUserIds) return new Set<string>();
    return excludeUserIds instanceof Set ? excludeUserIds : new Set(excludeUserIds);
  }, [excludeUserIds]);

  const query = {
    keyword: debouncedKeyword || undefined,
    pageIndex: 1,
    pageSize: SEARCH_PAGE_SIZE,
  };

  const candidatesQuery = useQuery({
    enabled: scope.isAvailable && !disabled,
    queryKey: projectManagementQueryKeys.memberCandidates(scope, query),
    queryFn: ({ signal }) => getProjectManagementMemberCandidates(query, signal),
  });

  const rawCandidates = candidatesQuery.data?.data?.items ?? [];

  useEffect(() => {
    onCandidatesChange?.(rawCandidates);
  }, [onCandidatesChange, rawCandidates]);

  useEffect(() => {
    onQueryStatusChange?.({
      isError: candidatesQuery.isError,
      refetch: () => {
        void candidatesQuery.refetch();
      },
    });
  }, [candidatesQuery.isError, candidatesQuery.refetch, onQueryStatusChange]);

  const options = useMemo(() => {
    return dedupeUserCandidates(rawCandidates).filter(
      (candidate) =>
        (candidate.isSelectable || candidate.userId === value) &&
        (!excludeSet.has(candidate.userId) || candidate.userId === value),
    );
  }, [excludeSet, rawCandidates, value]);

  useEffect(() => {
    if (!value) {
      setSelectedOption(null);
      return;
    }
    const match = options.find((item) => item.userId === value);
    if (match) setSelectedOption(match);
  }, [options, value]);

  const fieldLabel = label ?? t('projectManagement.members.form.user');
  const fieldPlaceholder = placeholder ?? t('projectManagement.members.form.searchPlaceholder');

  if (disabled && value) {
    return (
      <Box className="pm-member-user-readonly" aria-label={fieldLabel}>
        <Typography className="pm-member-user-readonly__label" component="span">
          {fieldLabel}
        </Typography>
        <Typography className="pm-member-user-readonly__name" component="div">
          {valueLabel ||
            (selectedOption ? formatMemberCandidate(selectedOption) : t('projectManagement.members.form.userUnavailable'))}
        </Typography>
      </Box>
    );
  }

  return (
    <Autocomplete
      disabled={disabled}
      filterOptions={(items) => items}
      getOptionLabel={(option) => formatMemberCandidate(option)}
      inputValue={inputValue}
      isOptionEqualToValue={(option, selectedValue) => option.userId === selectedValue.userId}
      loading={candidatesQuery.isFetching}
      noOptionsText={
        candidatesQuery.isError
          ? t('projectManagement.members.form.loadFailed')
          : candidatesQuery.isFetching
            ? t('projectManagement.members.form.searching')
            : t('projectManagement.members.form.noResults')
      }
      onChange={(_event, next) => {
        setSelectedOption(next);
        onUserChange(next?.userId ?? '', next);
        if (next) {
          setInputValue(formatMemberCandidate(next));
        }
      }}
      onInputChange={(_event, next, reason) => {
        if (reason === 'reset') return;
        setInputValue(next);
        if (reason === 'clear') {
          setSelectedOption(null);
          onUserChange('', null);
        }
      }}
      options={options}
      renderInput={(params) => (
        <PmFormInput
          {...params}
          aria-label={fieldLabel}
          label={fieldLabel}
          placeholder={fieldPlaceholder}
        />
      )}
      renderOption={(props, option) => {
        const { key, ...optionProps } = props as typeof props & { key?: string };
        const labels = buildMemberSearchLabel(option);
        return (
          <Box
            component="li"
            key={key ?? option.userId}
            {...optionProps}
            className={`pm-member-option ${optionProps.className ?? ''}`}
          >
            <Typography className="pm-member-option__primary" component="span">
              {labels.primary}
            </Typography>
            {labels.secondary ? (
              <Typography className="pm-member-option__meta" component="span">
                {labels.secondary}
              </Typography>
            ) : null}
          </Box>
        );
      }}
      value={selectedOption}
    />
  );
}
