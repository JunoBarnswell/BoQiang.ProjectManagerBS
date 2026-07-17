import { buildPreviewUrl } from '../../../api/system/files.api';
import { httpClient } from '../../../core/http/httpClient';

const ASSET_PREFIX = 'asset://';

function deepClone<T>(value: T): T {
  return value === undefined ? value : JSON.parse(JSON.stringify(value)) as T;
}

function collectAssetTokens(value: unknown, result: Set<string>) {
  if (typeof value === 'string' && value.startsWith(ASSET_PREFIX)) {
    result.add(value.slice(ASSET_PREFIX.length));
    return;
  }

  if (Array.isArray(value)) {
    value.forEach((item) => collectAssetTokens(item, result));
    return;
  }

  if (value && typeof value === 'object') {
    Object.values(value).forEach((item) => collectAssetTokens(item, result));
  }
}

function replaceAssetTokens(value: unknown, assetMap: Map<string, string>): unknown {
  if (typeof value === 'string' && value.startsWith(ASSET_PREFIX)) {
    const fileId = value.slice(ASSET_PREFIX.length);
    return assetMap.get(fileId) ?? value;
  }

  if (Array.isArray(value)) {
    return value.map((item) => replaceAssetTokens(item, assetMap));
  }

  if (value && typeof value === 'object') {
    return Object.fromEntries(
      Object.entries(value).map(([key, item]) => [key, replaceAssetTokens(item, assetMap)])
    );
  }

  return value;
}

function restoreAssetTokensInternal(value: unknown, reverseMap: Map<string, string>): unknown {
  if (typeof value === 'string' && reverseMap.has(value)) {
    return reverseMap.get(value) ?? value;
  }

  if (Array.isArray(value)) {
    return value.map((item) => restoreAssetTokensInternal(item, reverseMap));
  }

  if (value && typeof value === 'object') {
    return Object.fromEntries(
      Object.entries(value).map(([key, item]) => [key, restoreAssetTokensInternal(item, reverseMap)])
    );
  }

  return value;
}

export interface AssetResolutionResult<TData> {
  cleanup: () => void;
  resolvedData: TData;
  reverseMap: Map<string, string>;
}

export async function resolveAssetTokensInData<TData>(data: TData): Promise<AssetResolutionResult<TData>> {
  const cloned = deepClone(data);
  const tokens = new Set<string>();
  collectAssetTokens(cloned, tokens);

  const resolvedAssetMap = new Map<string, string>();
  const reverseMap = new Map<string, string>();

  await Promise.all(
    [...tokens].map(async (fileId) => {
      try {
        const response = await httpClient.downloadBlob(buildPreviewUrl(fileId), { timeoutMs: 120_000 });
        const objectUrl = URL.createObjectURL(response.blob);
        resolvedAssetMap.set(fileId, objectUrl);
        reverseMap.set(objectUrl, `${ASSET_PREFIX}${fileId}`);
      } catch {
        // Keep the asset token untouched when the file cannot be resolved.
      }
    })
  );

  return {
    cleanup: () => {
      resolvedAssetMap.forEach((objectUrl) => URL.revokeObjectURL(objectUrl));
    },
    resolvedData: replaceAssetTokens(cloned, resolvedAssetMap) as TData,
    reverseMap
  };
}

export function restoreAssetTokens<TData>(data: TData, reverseMap: Map<string, string>): TData {
  return restoreAssetTokensInternal(deepClone(data), reverseMap) as TData;
}

export function toAssetToken(fileId: string): string {
  return `${ASSET_PREFIX}${fileId}`;
}
