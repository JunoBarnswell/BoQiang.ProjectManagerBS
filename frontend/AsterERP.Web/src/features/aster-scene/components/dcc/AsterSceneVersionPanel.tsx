import { X } from 'lucide-react';

import { PermissionButton } from '@/shared/auth/PermissionButton';

import { asterScenePermissions } from '../../model/permissions';
import type { AsterScenePublishVersion } from '../../model/types';

interface AsterSceneVersionPanelProps {
  dirty: boolean;
  onClose: () => void;
  onRollback: (version: AsterScenePublishVersion) => void;
  pending: boolean;
  t: (key: string) => string;
  versions: AsterScenePublishVersion[];
}

export function AsterSceneVersionPanel({ dirty, onClose, onRollback, pending, t, versions }: AsterSceneVersionPanelProps) {
  return (
    <aside className="as-dcc-version-panel" aria-label={t('asterscene.studio.versions')}>
      <header>
        <h2>{t('asterscene.studio.versions')}</h2>
        <button aria-label={t('asterscene.studio.closeVersions')} className="as-icon-button" onClick={onClose} type="button">
          <X size={16} />
        </button>
      </header>
      {dirty ? <p className="as-dcc-muted">{t('asterscene.studio.rollbackDisabledDirty')}</p> : null}
      {versions.length === 0 ? <p className="as-dcc-muted">{t('asterscene.studio.noVersions')}</p> : null}
      <div className="as-dcc-version-list">
        {versions.map((version) => (
          <article className="as-dcc-version" key={version.id}>
            <div>
              <strong>v{version.version}</strong>
              <span>{version.publishCode}</span>
              <span>{t('asterscene.studio.versionStatus')}: {version.status} · {new Date(version.publishedAt).toLocaleString()}</span>
            </div>
            <PermissionButton
              className="as-button"
              code={asterScenePermissions.publishRollback}
              disabled={dirty || pending}
              iconStart={false}
              onClick={() => onRollback(version)}
              type="button"
            >
              {t('asterscene.studio.rollback')}
            </PermissionButton>
          </article>
        ))}
      </div>
    </aside>
  );
}
