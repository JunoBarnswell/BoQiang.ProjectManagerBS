import type { AsterSceneAsset, RuntimeManifest } from '../../model/types';

export interface ResourceRecord {
  assetId: string;
  bytes?: number | null;
  dispose: () => void;
  kind: string;
  status: 'error' | 'idle' | 'loaded' | 'loading';
  url?: string | null;
}

export class ResourceRegistry {
  private readonly resources = new Map<string, ResourceRecord>();

  public clear(): void {
    this.resources.forEach((resource) => resource.dispose());
    this.resources.clear();
  }

  public registerAsset(asset: AsterSceneAsset): ResourceRecord {
    const existing = this.resources.get(asset.id);
    if (existing) {
      return existing;
    }

    const record: ResourceRecord = {
      assetId: asset.id,
      bytes: asset.sizeBytes,
      dispose: () => undefined,
      kind: asset.assetType,
      status: asset.runtimeUrl ? 'loaded' : 'idle',
      url: asset.runtimeUrl
    };
    this.resources.set(asset.id, record);
    return record;
  }

  public hydrateManifest(manifest: RuntimeManifest): ResourceRecord[] {
    this.clear();
    return Object.entries(manifest.assetVariants).map(([assetId, variants]) => {
      const firstVariant = variants[0] ?? null;
      const runtimeUrl = firstVariant?.runtimeUrl ?? firstVariant?.sourceUrl ?? null;
      const record: ResourceRecord = {
        assetId,
        bytes: typeof firstVariant?.sizeBytes === 'number' ? firstVariant.sizeBytes : null,
        dispose: () => undefined,
        kind: String(firstVariant?.variantType ?? 'runtime'),
        status: runtimeUrl ? 'loaded' : 'idle',
        url: runtimeUrl
      };
      this.resources.set(assetId, record);
      return record;
    });
  }

  public list(): ResourceRecord[] {
    return Array.from(this.resources.values());
  }
}
