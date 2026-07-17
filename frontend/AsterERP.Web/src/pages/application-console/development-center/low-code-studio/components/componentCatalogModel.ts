import type { ComponentManifest } from './ComponentManifest';

export function filterComponentCatalog(manifests: readonly ComponentManifest[], query: string): ComponentManifest[] {
  const normalizedQuery = query.trim().toLocaleLowerCase();
  const matches = manifests.filter((manifest) => !normalizedQuery || manifest.type.toLocaleLowerCase().includes(normalizedQuery));
  return [...matches].sort((left, right) => left.type.localeCompare(right.type));
}
