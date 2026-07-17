import { useQuery } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';

import { useI18n } from '@/core/i18n/I18nProvider';

import { asterSceneApi } from '../api/asterScene.api';
import { AsterSceneLayout } from '../components/AsterSceneLayout';
import { AsterSceneState } from '../components/AsterSceneState';
import { WorkGrid } from '../components/WorkGrid';

export function AsterSceneCreatorPage() {
  const { translate: t } = useI18n();
  const { handle = '' } = useParams();
  const profileQuery = useQuery({
    enabled: Boolean(handle),
    queryFn: ({ signal }) => asterSceneApi.public.creator(handle, signal),
    queryKey: ['asterscene', 'creator', handle]
  });
  const worksQuery = useQuery({
    enabled: Boolean(handle),
    queryFn: ({ signal }) => asterSceneApi.public.explore({ creatorHandle: handle, pageSize: 36 }, signal),
    queryKey: ['asterscene', 'creator-works', handle]
  });
  const profile = profileQuery.data?.data;

  if (profileQuery.isLoading) {
    return <AsterSceneState title={t('asterscene.creator.loading')} />;
  }

  if (profileQuery.isError || !profile) {
    return <AsterSceneState title={t('asterscene.creator.notFound')} />;
  }

  return (
    <AsterSceneLayout eyebrow={`@${profile.handle}`} title={profile.displayName}>
      <section className="as-band">
        <p>{profile.bio || t('asterscene.creator.defaultBio')}</p>
        <div className="as-metrics">
          <span>{profile.worksCount} {t('asterscene.creator.works')}</span>
          <span>{profile.followersCount} {t('asterscene.creator.followers')}</span>
        </div>
      </section>
      <WorkGrid works={worksQuery.data?.data.items ?? []} />
    </AsterSceneLayout>
  );
}
