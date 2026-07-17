import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Flag, Heart, Layers, Play, Star } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';

import { useI18n } from '@/core/i18n/I18nProvider';
import { PermissionButton } from '@/shared/auth/PermissionButton';

import { asterSceneApi } from '../api/asterScene.api';
import { AsterSceneLayout } from '../components/AsterSceneLayout';
import { AsterSceneState } from '../components/AsterSceneState';
import { createClientMutationId } from '../core/scene-document/documentKernel';
import { asterScenePermissions } from '../model/permissions';

export function AsterSceneWorkPage() {
  const { translate: t } = useI18n();
  const { slug = '' } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [remixName, setRemixName] = useState('');
  const workQuery = useQuery({
    enabled: Boolean(slug),
    queryFn: ({ signal }) => asterSceneApi.public.work(slug, signal),
    queryKey: ['asterscene', 'work', slug]
  });
  const work = workQuery.data?.data;
  const mutationOptions = useMemo(
    () => ({
      onSuccess: () => queryClient.invalidateQueries({ queryKey: ['asterscene', 'work', slug] })
    }),
    [queryClient, slug]
  );
  const likeMutation = useMutation({
    mutationFn: () => asterSceneApi.community.like(work?.id ?? '', createClientMutationId('like')),
    ...mutationOptions
  });
  const favoriteMutation = useMutation({
    mutationFn: () => asterSceneApi.community.favorite(work?.id ?? '', createClientMutationId('favorite')),
    ...mutationOptions
  });
  const reportMutation = useMutation({
    mutationFn: () =>
      asterSceneApi.community.report(work?.id ?? '', {
        clientMutationId: createClientMutationId('report'),
        reasonCode: 'unsafe-content'
      }),
    ...mutationOptions
  });
  const remixMutation = useMutation({
    mutationFn: () =>
      asterSceneApi.community.remix(work?.id ?? '', {
        clientMutationId: createClientMutationId('remix'),
        projectName: remixName || `${t('asterscene.work.remixPrefix')} ${work?.title ?? ''}`
      }),
    onSuccess: (response) => navigate(`/studio/${response.data.project.id}`)
  });

  if (workQuery.isLoading) {
    return <AsterSceneState title={t('asterscene.work.loading')} />;
  }

  if (workQuery.isError || !work) {
    return <AsterSceneState title={t('asterscene.work.notFound')} />;
  }

  return (
    <AsterSceneLayout eyebrow={`${t('asterscene.work.by')} ${work.creatorHandle}`} title={work.title}>
      <section className="as-band as-band--split">
        <div className="as-hero-preview">
          <span>{work.title.slice(0, 2).toUpperCase()}</span>
        </div>
        <section className="as-panel">
          <p>{work.summary || t('asterscene.work.defaultSummary')}</p>
          <div className="as-metrics">
            <span>{work.viewCount} {t('asterscene.work.views')}</span>
            <span>{work.likeCount} {t('asterscene.work.likes')}</span>
            <span>{work.favoriteCount} {t('asterscene.work.favorites')}</span>
            <span>{work.remixCount} {t('asterscene.work.remixes')}</span>
          </div>
          <div className="as-button-row">
            <Link className="as-button as-button--primary" to={`/player/${work.publishCode}`}>
              <Play size={16} /> {t('asterscene.work.play')}
            </Link>
            <PermissionButton
              className="as-button"
              code={asterScenePermissions.communityInteract}
              disabled={likeMutation.isPending}
              onClick={() => likeMutation.mutate()}
              type="button"
            >
              <Heart size={16} /> {t('asterscene.work.like')}
            </PermissionButton>
            <PermissionButton
              className="as-button"
              code={asterScenePermissions.communityInteract}
              disabled={favoriteMutation.isPending}
              onClick={() => favoriteMutation.mutate()}
              type="button"
            >
              <Star size={16} /> {t('asterscene.work.favorite')}
            </PermissionButton>
            <PermissionButton
              className="as-button"
              code={asterScenePermissions.communityInteract}
              disabled={reportMutation.isPending}
              onClick={() => reportMutation.mutate()}
              type="button"
            >
              <Flag size={16} /> {t('asterscene.work.report')}
            </PermissionButton>
          </div>
          <form
            className="as-remix"
            onSubmit={(event) => {
              event.preventDefault();
              remixMutation.mutate();
            }}
          >
            <input value={remixName} onChange={(event) => setRemixName(event.target.value)} placeholder={t('asterscene.work.remixPlaceholder')} />
            <PermissionButton
              className="as-button"
              code={asterScenePermissions.remixCreate}
              disabled={remixMutation.isPending}
              type="submit"
            >
              <Layers size={16} /> {t('asterscene.work.remix')}
            </PermissionButton>
          </form>
        </section>
      </section>
    </AsterSceneLayout>
  );
}
