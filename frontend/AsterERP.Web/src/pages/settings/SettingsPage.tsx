import { appEnv } from '../../core/config/env';
import { useI18n } from '../../core/i18n/I18nProvider';
import { useThemeStore } from '../../core/state';
import { ResponsivePanel } from '../../shared/layout/ResponsivePanel';
import { ResponsivePage } from '../../shared/responsive/ResponsivePage';

export function SettingsPage() {
  const { locale, translate } = useI18n();
  const theme = useThemeStore((state) => state.theme);

  return (
    <ResponsivePage description={translate('page.settings.description')} eyebrow={translate('nav.settings')} title={translate('page.settings.title')}>
      <ResponsivePanel>
        <div className="card-header">
          <div>
            <p className="card-kicker">{translate('nav.settings')}</p>
            <h2>{translate('page.settings.title')}</h2>
          </div>
        </div>

        <p className="muted">{translate('page.settings.description')}</p>

        <dl className="kv-grid env-grid">
          <div>
            <dt>{translate('settings.currentMode')}</dt>
            <dd>{appEnv.mode}</dd>
          </div>
          <div>
            <dt>{translate('toolbar.locale')}</dt>
            <dd>{locale}</dd>
          </div>
          <div>
            <dt>{translate('settings.apiBaseUrl')}</dt>
            <dd>{appEnv.apiBaseUrl}</dd>
          </div>
          <div>
            <dt>{translate('toolbar.theme')}</dt>
            <dd>{theme}</dd>
          </div>
        </dl>
      </ResponsivePanel>
    </ResponsivePage>
  );
}
