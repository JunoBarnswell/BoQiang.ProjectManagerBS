import * as THREE from 'three';

import type { SceneVector3 } from '../../../model/types';

export function readVector(value: unknown, fallback: SceneVector3): THREE.Vector3 {
  if (!value || typeof value !== 'object') {
    return new THREE.Vector3(fallback.x, fallback.y, fallback.z);
  }

  const record = value as Record<string, unknown>;
  return new THREE.Vector3(readNumber(record.x, fallback.x), readNumber(record.y, fallback.y), readNumber(record.z, fallback.z));
}

export function readNumber(value: unknown, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

export function readString(value: unknown, fallback = ''): string {
  return typeof value === 'string' ? value : fallback;
}

export function readOptionalString(value: unknown): string | null {
  return typeof value === 'string' && value.length > 0 ? value : null;
}

export function readColor(value: unknown, fallback: number): THREE.ColorRepresentation {
  return typeof value === 'string' && /^#[0-9a-f]{6}$/i.test(value) ? value : fallback;
}

export function readPbrValue(material: Record<string, unknown> | undefined, key: string): unknown {
  const pbr = material?.pbr;
  return pbr && typeof pbr === 'object' ? (pbr as Record<string, unknown>)[key] : undefined;
}
