import { Eye, Heart, Layers, Star } from 'lucide-react';
import { Link } from 'react-router-dom';

import { useI18n } from '@/core/i18n/I18nProvider';

import type { AsterScenePublicWork } from '../model/types';

interface WorkGridProps {
  works: AsterScenePublicWork[];
}

export function WorkGrid({ works }: WorkGridProps) {
  const { translate: t } = useI18n();

  return (
    <section className="as-work-grid">
      {works.map((work) => (
        <article className="as-work" key={work.id}>
          <Link className="as-work__preview" to={`/works/${work.slug}`}>
            <span>{work.title.slice(0, 2).toUpperCase()}</span>
          </Link>
          <div className="as-work__body">
            <Link to={`/works/${work.slug}`}>{work.title}</Link>
            <p>{work.summary || t('asterscene.workGrid.defaultSummary')}</p>
            <div className="as-work__meta">
              <span>
                <Eye size={14} /> {work.viewCount}
              </span>
              <span>
                <Heart size={14} /> {work.likeCount}
              </span>
              <span>
                <Star size={14} /> {work.favoriteCount}
              </span>
              <span>
                <Layers size={14} /> {work.remixCount}
              </span>
            </div>
          </div>
        </article>
      ))}
    </section>
  );
}
