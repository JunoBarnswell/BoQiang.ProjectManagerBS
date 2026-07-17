import * as THREE from 'three';

export function disposeObject3D(root: THREE.Object3D): void {
  root.traverse((object) => {
    const geometry = (object as THREE.Object3D & { geometry?: THREE.BufferGeometry }).geometry;
    if (geometry) {
      disposeGeometry(geometry);
    }

    const material = (object as THREE.Object3D & { material?: THREE.Material | THREE.Material[] }).material;
    if (material) {
      disposeObjectMaterial(material);
    }

    if (object instanceof THREE.SkinnedMesh) {
      object.skeleton.dispose();
    }
  });
}

function disposeGeometry(geometry: THREE.BufferGeometry): void {
  const maybeBvhGeometry = geometry as THREE.BufferGeometry & { disposeBoundsTree?: () => void };
  maybeBvhGeometry.disposeBoundsTree?.();
  geometry.dispose();
}

export function disposeObjectMaterial(material: THREE.Material | THREE.Material[]): void {
  if (Array.isArray(material)) {
    material.forEach(disposeMaterial);
    return;
  }

  disposeMaterial(material);
}

export function disposeMaterial(material: THREE.Material): void {
  material.userData.disposed = true;
  Object.values(material).forEach((value) => {
    if (value instanceof THREE.Texture) {
      value.dispose();
    }
  });
  material.dispose();
}
