import { describe, expect, it } from 'vitest';

import { HalfEdgeMesh } from './HalfEdgeMesh';

describe('HalfEdgeMesh', () => {
  it('extrudes and insets selected faces with valid topology summary', () => {
    const mesh = HalfEdgeMesh.box(1, 1, 1);
    const edited = mesh.insetFaces([0], 0.2).extrudeFaces([0], 0.3);
    const result = edited.validate();

    expect(result.errors).toEqual([]);
    expect(result.summary.vertexCount).toBeGreaterThan(mesh.topologySummary().vertexCount);
    expect(result.summary.faceCount).toBeGreaterThan(mesh.topologySummary().faceCount);
  });

  it('bridges two edges and can flip normals without losing faces', () => {
    const mesh = HalfEdgeMesh.box(1, 1, 1);
    const bridged = mesh.bridgeEdges(0, 2);
    const flipped = bridged.flipFaces([0]);

    expect(bridged.topologySummary().faceCount).toBe(mesh.topologySummary().faceCount + 1);
    expect(flipped.topologySummary().faceCount).toBe(bridged.topologySummary().faceCount);
    expect(flipped.toPayload().faces[0]).toHaveProperty('vertices');
  });

  it('welds, collapses, detaches, and subdivides without producing empty geometry', () => {
    const mesh = HalfEdgeMesh.box(1, 1, 1);
    const edited = mesh
      .detachFaces([0])
      .weldVertices([0, 1, 2, 3], 0.01)
      .collapseEdges([0])
      .subdivideFaces([0]);
    const summary = edited.topologySummary();

    expect(summary.vertexCount).toBeGreaterThanOrEqual(3);
    expect(summary.faceCount).toBeGreaterThan(0);
    expect(edited.validate().errors).toEqual([]);
  });

  it('serializes face objects and reads legacy face arrays', () => {
    const payload = HalfEdgeMesh.plane(2, 2).toPayload();
    const roundTripped = new HalfEdgeMesh(payload);
    const legacy = new HalfEdgeMesh({
      faces: [[0, 1, 2]],
      vertices: [
        { x: 0, y: 0, z: 0 },
        { x: 1, y: 0, z: 0 },
        { x: 0, y: 1, z: 0 }
      ]
    });

    expect(payload.faces[0]).toHaveProperty('vertices');
    expect(roundTripped.topologySummary()).toEqual(HalfEdgeMesh.plane(2, 2).topologySummary());
    expect(legacy.validate().errors).toEqual([]);
  });
});
