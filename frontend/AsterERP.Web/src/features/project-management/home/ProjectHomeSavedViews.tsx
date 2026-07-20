import { useEffect, useState } from 'react';

import type { ProjectManagementSavedView } from '../../../api/project-management/projectManagement.types';
import { PmBox, PmButton, PmFormInput, PmPopover, PmText } from '../../../ui/project-management';

interface ProjectHomeSavedViewsProps {
  views: ProjectManagementSavedView[];
  pending?: boolean;
  onApply: (view: ProjectManagementSavedView) => void;
  onCreate: (name: string) => void;
  onDelete?: (view: ProjectManagementSavedView) => void;
  autoOpen?: boolean;
  onClose?: () => void;
  translate: (key: string) => string;
}

export function ProjectHomeSavedViews({ views, pending, onApply, onCreate, onDelete, autoOpen = false, onClose, translate }: ProjectHomeSavedViewsProps) {
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const [name, setName] = useState('');
  const submit = () => {
    const value = name.trim();
    if (!value || pending) return;
    onCreate(value);
    setName('');
  };
  useEffect(() => {
    if (!autoOpen) return;
    const trigger = document.querySelector<HTMLElement>('[data-home-saved-views-trigger="true"]');
    if (trigger) setAnchor(trigger);
  }, [autoOpen]);
  const close = () => { setAnchor(null); onClose?.(); };
  return <>
    <PmButton data-home-saved-views-trigger="true" onClick={event => setAnchor(event.currentTarget)}>{translate('projectManagement.home.views')}</PmButton>
    <PmPopover anchorEl={anchor} onClose={close} open={Boolean(anchor)}>
      <PmBox sx={{ p: 1.5, width: 280, display: 'grid', gap: 1 }}>
        <PmText fontSize=".76rem" fontWeight={700}>{translate('projectManagement.home.savedViews')}</PmText>
        {views.length === 0 ? <PmText color="text.secondary" fontSize=".76rem">{translate('projectManagement.home.noSavedViews')}</PmText> : views.map(view => <PmBox key={view.id} sx={{ display: 'flex', alignItems: 'center', gap: .5 }}><PmButton onClick={() => { onApply(view); setAnchor(null); }} sx={{ justifyContent: 'flex-start', flex: 1 }}>{view.viewName}</PmButton>{onDelete && <PmButton aria-label={`${translate('projectManagement.home.deleteSavedView')}: ${view.viewName}`} color="error" onClick={() => onDelete(view)} size="small">×</PmButton>}</PmBox>)}
        <PmBox sx={{ display: 'flex', gap: .75, alignItems: 'center' }}>
          <PmFormInput aria-label={translate('projectManagement.home.savedViewName')} onChange={event => setName(event.target.value)} onKeyDown={event => { if (event.key === 'Enter') submit(); }} placeholder={translate('projectManagement.home.savedViewName')} size="small" value={name} />
          <PmButton disabled={!name.trim() || pending} onClick={submit}>{translate('projectManagement.home.saveView')}</PmButton>
        </PmBox>
      </PmBox>
    </PmPopover>
  </>;
}
