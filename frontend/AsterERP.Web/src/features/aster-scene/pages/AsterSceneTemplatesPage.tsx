import { useQuery } from '@tanstack/react-query';

import { useI18n } from '@/core/i18n/I18nProvider';

import { asterSceneApi } from '../api/asterScene.api';
import { AsterSceneLayout } from '../components/AsterSceneLayout';
import { AsterSceneState } from '../components/AsterSceneState';
import { WorkGrid } from '../components/WorkGrid';

export function AsterSceneTemplatesPage() {
  const { translate: t } = useI18n();
  const query = useQuery({
    queryFn: ({ signal }) => asterSceneApi.public.templates({ pageSize: 36 }, signal),
    queryKey: ['asterscene', 'templates']
  });
  const works = query.data?.data.items ?? [];

  return (
    <AsterSceneLayout eyebrow={t('asterscene.templates.eyebrow')} title={t('asterscene.templates.title')}>
      {query.isLoading ? <AsterSceneState title={t('asterscene.templates.loading')} /> : null}
      {query.isError ? <AsterSceneState title={t('asterscene.templates.failed')} /> : null}
      {!query.isLoading && works.length === 0 ? <AsterSceneState title={t('asterscene.templates.empty')} /> : <WorkGrid works={works} />}
    </AsterSceneLayout>
  );
}
