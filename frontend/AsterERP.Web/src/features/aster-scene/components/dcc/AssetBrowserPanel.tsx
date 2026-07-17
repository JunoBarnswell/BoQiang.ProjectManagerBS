import { FileBox, Image, PackagePlus } from 'lucide-react';

import type { AsterSceneAsset } from '../../model/types';

interface AssetBrowserPanelProps {
  assets: AsterSceneAsset[];
  onCreateMaterialFromAsset: (asset: AsterSceneAsset) => void;
  onPlaceAsset: (asset: AsterSceneAsset) => void;
  t: (key: string) => string;
}

export function AssetBrowserPanel({ assets, onCreateMaterialFromAsset, onPlaceAsset, t }: AssetBrowserPanelProps) {
  const grouped = assets.reduce<Record<string, AsterSceneAsset[]>>((current, asset) => {
    const key = asset.assetType.toLowerCase();
    current[key] = [...(current[key] ?? []), asset];
    return current;
  }, {});

  return (
    <section className="as-dcc-asset-panel">
      <header>
        <h2>{t('asterscene.dcc.assetBrowser')}</h2>
        <span>{assets.length}</span>
      </header>
      {Object.entries(grouped).map(([type, items]) => (
        <div className="as-dcc-asset-group" key={type}>
          <h3>{t(`asterscene.assetType.${type}`)}</h3>
          {items.map((asset) => (
            <div className="as-dcc-asset" draggable key={asset.id}>
              <div className="as-dcc-asset__thumb">
                {asset.thumbnailUrl ? <img alt="" src={asset.thumbnailUrl} /> : type === 'texture' || type === 'panorama' || type === 'image' ? <Image size={18} /> : <FileBox size={18} />}
              </div>
              <div>
                <strong>{asset.fileName}</strong>
                <span>{asset.status}</span>
              </div>
              <button onClick={() => (type === 'texture' || type === 'material' ? onCreateMaterialFromAsset(asset) : onPlaceAsset(asset))} type="button">
                <PackagePlus size={15} />
              </button>
            </div>
          ))}
        </div>
      ))}
      {assets.length === 0 ? <p className="as-dcc-muted">{t('asterscene.dcc.assets.empty')}</p> : null}
    </section>
  );
}
