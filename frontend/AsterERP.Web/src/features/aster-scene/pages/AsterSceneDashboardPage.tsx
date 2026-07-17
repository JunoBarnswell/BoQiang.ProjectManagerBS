import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ExternalLink, Plus } from 'lucide-react';
import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';

import { useI18n } from '@/core/i18n/I18nProvider';
import { PermissionButton } from '@/shared/auth/PermissionButton';

import { asterSceneApi } from '../api/asterScene.api';
import { AsterSceneLayout } from '../components/AsterSceneLayout';
import { AsterSceneState } from '../components/AsterSceneState';
import { createClientMutationId } from '../core/scene-document/documentKernel';
import { asterScenePermissions } from '../model/permissions';

export function AsterSceneDashboardPage() {
  const { translate: t } = useI18n();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [projectName, setProjectName] = useState('');
  const [description, setDescription] = useState('');
  const projectsQuery = useQuery({
    queryFn: ({ signal }) => asterSceneApi.projects.list({ pageSize: 50 }, signal),
    queryKey: ['asterscene', 'projects']
  });
  const createMutation = useMutation({
    mutationFn: () =>
      asterSceneApi.projects.create({
        clientMutationId: createClientMutationId('project'),
        description,
        projectName,
        visibility: 'Private'
      }),
    onSuccess: async (response) => {
      await queryClient.invalidateQueries({ queryKey: ['asterscene', 'projects'] });
      setProjectName('');
      setDescription('');
      navigate(`/studio/${response.data.id}`);
    }
  });

  const projects = projectsQuery.data?.data.items ?? [];

  return (
    <AsterSceneLayout eyebrow={t('asterscene.dashboard.eyebrow')} title={t('asterscene.dashboard.title')}>
      <section className="as-band as-band--split">
        <form
          className="as-panel"
          onSubmit={(event) => {
            event.preventDefault();
            if (projectName.trim()) {
              createMutation.mutate();
            }
          }}
        >
          <h2>{t('asterscene.dashboard.createProject')}</h2>
          <label>
            {t('asterscene.common.name')}
            <input value={projectName} onChange={(event) => setProjectName(event.target.value)} placeholder={t('asterscene.dashboard.projectPlaceholder')} />
          </label>
          <label>
            {t('asterscene.common.description')}
            <textarea value={description} onChange={(event) => setDescription(event.target.value)} rows={4} />
          </label>
          <PermissionButton
            className="as-button as-button--primary"
            code={asterScenePermissions.projectCreate}
            disabled={!projectName.trim() || createMutation.isPending}
            iconStart={false}
            type="submit"
          >
            <Plus size={16} /> {t('asterscene.common.create')}
          </PermissionButton>
        </form>

        <section className="as-panel as-panel--wide">
          <h2>{t('asterscene.dashboard.projects')}</h2>
          {projectsQuery.isLoading ? <AsterSceneState title={t('asterscene.dashboard.loadingProjects')} /> : null}
          {projectsQuery.isError ? <AsterSceneState title={t('asterscene.dashboard.projectsFailed')} description={t('asterscene.dashboard.projectsFailedDescription')} /> : null}
          {!projectsQuery.isLoading && projects.length === 0 ? <AsterSceneState title={t('asterscene.dashboard.noProjects')} /> : null}
          <div className="as-table">
            {projects.map((project) => (
              <div className="as-table__row" key={project.id}>
                <div>
                  <strong>{project.projectName}</strong>
                  <span>{project.projectCode}</span>
                </div>
                <span>{project.status}</span>
                <span>r{project.currentRevision}</span>
                <Link className="as-icon-link" to={`/studio/${project.id}`}>
                  <ExternalLink size={16} /> {t('asterscene.common.open')}
                </Link>
              </div>
            ))}
          </div>
        </section>
      </section>
    </AsterSceneLayout>
  );
}
