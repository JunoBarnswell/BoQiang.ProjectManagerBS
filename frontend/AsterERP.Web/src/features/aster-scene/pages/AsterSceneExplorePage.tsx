import { useQuery } from '@tanstack/react-query';
import { Search } from 'lucide-react';
import { useState } from 'react';

import { useI18n } from '@/core/i18n/I18nProvider';

import { asterSceneApi } from '../api/asterScene.api';
import { AsterSceneLayout } from '../components/AsterSceneLayout';
import { AsterSceneState } from '../components/AsterSceneState';
import { WorkGrid } from '../components/WorkGrid';

export function AsterSceneExplorePage() {
  const { translate: t } = useI18n();
  const [keyword, setKeyword] = useState('');
  const exploreQuery = useQuery({
    queryFn: ({ signal }) => asterSceneApi.public.explore({ keyword, pageSize: 36 }, signal),
    queryKey: ['asterscene', 'explore', keyword]
  });
  const works = exploreQuery.data?.data.items ?? [];

  return (
    <AsterSceneLayout
      actions={
        <label className="as-search">
          <Search size={16} />
          <input value={keyword} onChange={(event) => setKeyword(event.target.value)} placeholder={t('asterscene.explore.searchPlaceholder')} />
        </label>
      }
      eyebrow={t('asterscene.explore.eyebrow')}
      title={t('asterscene.explore.title')}
    >
      {exploreQuery.isLoading ? <AsterSceneState title={t('asterscene.explore.loadingWorks')} /> : null}
      {exploreQuery.isError ? <AsterSceneState title={t('asterscene.explore.failed')} description={t('asterscene.explore.failedDescription')} /> : null}
      {!exploreQuery.isLoading && works.length === 0 ? <AsterSceneState title={t('asterscene.explore.noWorks')} /> : <WorkGrid works={works} />}
    </AsterSceneLayout>
  );
}
