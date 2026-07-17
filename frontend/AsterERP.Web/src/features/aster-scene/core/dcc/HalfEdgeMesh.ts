import type { SceneEditableMeshPayload, SceneTopologySummary, SceneVector3 } from '../../model/types';

export interface MeshValidationResult {
  errors: string[];
  summary: SceneTopologySummary;
  warnings: string[];
}

export interface MeshSelection {
  edges?: number[];
  faces?: number[];
  vertices?: number[];
}

interface EdgeUse {
  a: number;
  b: number;
  count: number;
}

export class HalfEdgeMesh {
  public readonly faces: number[][];
  public readonly materialIndices: number[];
  public readonly vertices: SceneVector3[];

  public constructor(payload: SceneEditableMeshPayload) {
    this.vertices = payload.vertices.map(cloneVector);
    this.faces = payload.faces.map(readFaceVertices);
    this.materialIndices = payload.materialIndices?.length === this.faces.length ? [...payload.materialIndices] : this.faces.map(() => 0);
  }

  public static box(width = 1, height = 1, depth = 1): HalfEdgeMesh {
    const x = width / 2;
    const y = height / 2;
    const z = depth / 2;
    return new HalfEdgeMesh({
      faces: [
        [0, 1, 2, 3],
        [5, 4, 7, 6],
        [4, 0, 3, 7],
        [1, 5, 6, 2],
        [3, 2, 6, 7],
        [4, 5, 1, 0]
      ],
      vertices: [
        { x: -x, y: -y, z: z },
        { x, y: -y, z: z },
        { x, y, z },
        { x: -x, y, z },
        { x: -x, y: -y, z: -z },
        { x, y: -y, z: -z },
        { x, y, z: -z },
        { x: -x, y, z: -z }
      ]
    });
  }

  public static plane(width = 1, depth = 1): HalfEdgeMesh {
    const x = width / 2;
    const z = depth / 2;
    return new HalfEdgeMesh({
      faces: [[0, 1, 2, 3]],
      vertices: [
        { x: -x, y: 0, z },
        { x, y: 0, z },
        { x, y: 0, z: -z },
        { x: -x, y: 0, z: -z }
      ]
    });
  }

  public bridgeEdges(firstEdgeIndex: number, secondEdgeIndex: number): HalfEdgeMesh {
    const edges = this.readUniqueEdges();
    const first = edges[firstEdgeIndex];
    const second = edges[secondEdgeIndex];
    if (!first || !second) {
      return this.clone();
    }

    return this.withFaces([...this.faces, [first.a, first.b, second.b, second.a]], [...this.materialIndices, 0]);
  }

  public collapseEdges(edgeIndices: number[]): HalfEdgeMesh {
    const edges = this.readUniqueEdges();
    const vertices = this.vertices.map(cloneVector);
    const replace = new Map<number, number>();
    edgeIndices.forEach((edgeIndex) => {
      const edge = edges[edgeIndex];
      if (!edge) {
        return;
      }

      const midpoint = averageVector(vertices[edge.a], vertices[edge.b]);
      vertices[edge.a] = midpoint;
      replace.set(edge.b, edge.a);
    });

    const faces = this.faces
      .map((face) => compactFace(face.map((vertexIndex) => replace.get(vertexIndex) ?? vertexIndex)))
      .filter((face) => face.length >= 3);
    return new HalfEdgeMesh({ faces, materialIndices: this.materialIndices.slice(0, faces.length), vertices });
  }

  public detachFaces(faceIndices: number[]): HalfEdgeMesh {
    const selected = new Set(faceIndices);
    const vertices = this.vertices.map(cloneVector);
    const faces = this.faces.map((face, faceIndex) => {
      if (!selected.has(faceIndex)) {
        return [...face];
      }

      return face.map((vertexIndex) => {
        vertices.push(cloneVector(this.vertices[vertexIndex]));
        return vertices.length - 1;
      });
    });
    return new HalfEdgeMesh({ faces, materialIndices: this.materialIndices, vertices });
  }

  public extrudeFaces(faceIndices: number[], distance = 0.25): HalfEdgeMesh {
    const selected = new Set(faceIndices);
    const vertices = this.vertices.map(cloneVector);
    const faces: number[][] = [];
    const materialIndices: number[] = [];

    this.faces.forEach((face, faceIndex) => {
      if (!selected.has(faceIndex)) {
        faces.push([...face]);
        materialIndices.push(this.materialIndices[faceIndex] ?? 0);
        return;
      }

      const normal = faceNormal(face, this.vertices);
      const extruded = face.map((vertexIndex) => {
        const source = this.vertices[vertexIndex];
        vertices.push({
          x: source.x + normal.x * distance,
          y: source.y + normal.y * distance,
          z: source.z + normal.z * distance
        });
        return vertices.length - 1;
      });

      faces.push(extruded);
      materialIndices.push(this.materialIndices[faceIndex] ?? 0);
      for (let index = 0; index < face.length; index += 1) {
        const next = (index + 1) % face.length;
        faces.push([face[index], face[next], extruded[next], extruded[index]]);
        materialIndices.push(this.materialIndices[faceIndex] ?? 0);
      }
    });

    return new HalfEdgeMesh({ faces, materialIndices, vertices });
  }

  public flipFaces(faceIndices?: number[]): HalfEdgeMesh {
    const selected = faceIndices ? new Set(faceIndices) : null;
    return this.withFaces(
      this.faces.map((face, index) => (selected === null || selected.has(index) ? [...face].reverse() : [...face])),
      this.materialIndices
    );
  }

  public insetFaces(faceIndices: number[], amount = 0.12): HalfEdgeMesh {
    const selected = new Set(faceIndices);
    const vertices = this.vertices.map(cloneVector);
    const faces: number[][] = [];
    const materialIndices: number[] = [];

    this.faces.forEach((face, faceIndex) => {
      if (!selected.has(faceIndex)) {
        faces.push([...face]);
        materialIndices.push(this.materialIndices[faceIndex] ?? 0);
        return;
      }

      const center = faceCenter(face, this.vertices);
      const inset = face.map((vertexIndex) => {
        const source = this.vertices[vertexIndex];
        vertices.push({
          x: source.x + (center.x - source.x) * amount,
          y: source.y + (center.y - source.y) * amount,
          z: source.z + (center.z - source.z) * amount
        });
        return vertices.length - 1;
      });

      faces.push(inset);
      materialIndices.push(this.materialIndices[faceIndex] ?? 0);
      for (let index = 0; index < face.length; index += 1) {
        const next = (index + 1) % face.length;
        faces.push([face[index], face[next], inset[next], inset[index]]);
        materialIndices.push(this.materialIndices[faceIndex] ?? 0);
      }
    });

    return new HalfEdgeMesh({ faces, materialIndices, vertices });
  }

  public subdivideFaces(faceIndices?: number[]): HalfEdgeMesh {
    const selected = faceIndices ? new Set(faceIndices) : null;
    const vertices = this.vertices.map(cloneVector);
    const faces: number[][] = [];
    const materialIndices: number[] = [];

    this.faces.forEach((face, faceIndex) => {
      if (selected && !selected.has(faceIndex)) {
        faces.push([...face]);
        materialIndices.push(this.materialIndices[faceIndex] ?? 0);
        return;
      }

      const center = faceCenter(face, this.vertices);
      vertices.push(center);
      const centerIndex = vertices.length - 1;
      for (let index = 0; index < face.length; index += 1) {
        faces.push([face[index], face[(index + 1) % face.length], centerIndex]);
        materialIndices.push(this.materialIndices[faceIndex] ?? 0);
      }
    });

    return new HalfEdgeMesh({ faces, materialIndices, vertices });
  }

  public weldVertices(vertexIndices: number[], tolerance = 0.001): HalfEdgeMesh {
    const selected = new Set(vertexIndices);
    const replace = new Map<number, number>();
    for (let outer = 0; outer < this.vertices.length; outer += 1) {
      if (!selected.has(outer)) {
        continue;
      }

      for (let inner = outer + 1; inner < this.vertices.length; inner += 1) {
        if (selected.has(inner) && vectorDistance(this.vertices[outer], this.vertices[inner]) <= tolerance) {
          replace.set(inner, outer);
        }
      }
    }

    const faces = this.faces
      .map((face) => compactFace(face.map((vertexIndex) => replace.get(vertexIndex) ?? vertexIndex)))
      .filter((face) => face.length >= 3);
    return new HalfEdgeMesh({ faces, materialIndices: this.materialIndices.slice(0, faces.length), vertices: this.vertices });
  }

  public validate(): MeshValidationResult {
    const summary = this.topologySummary();
    const errors: string[] = [];
    const warnings: string[] = [];
    if (summary.vertexCount < 3) {
      errors.push('Mesh requires at least three vertices.');
    }

    if (summary.faceCount === 0) {
      errors.push('Mesh requires at least one face.');
    }

    if (summary.nonManifoldEdges > 0) {
      errors.push('Mesh contains non-manifold edges.');
    }

    if (summary.boundaryEdges > 0) {
      warnings.push('Mesh contains open boundary edges.');
    }

    return { errors, summary, warnings };
  }

  public topologySummary(): SceneTopologySummary {
    const edges = this.readEdgeUses();
    return {
      boundaryEdges: edges.filter((edge) => edge.count === 1).length,
      edgeCount: edges.length,
      faceCount: this.faces.length,
      nonManifoldEdges: edges.filter((edge) => edge.count > 2).length,
      vertexCount: this.vertices.length
    };
  }

  public toPayload(): SceneEditableMeshPayload {
    return {
      edges: this.readUniqueEdges().map((edge) => [edge.a, edge.b]),
      faces: this.faces.map((face, index) => ({
        materialIndex: this.materialIndices[index] ?? 0,
        normal: faceNormal(face, this.vertices),
        vertices: [...face]
      })),
      materialIndices: [...this.materialIndices],
      vertices: this.vertices.map(cloneVector)
    };
  }

  private clone(): HalfEdgeMesh {
    return new HalfEdgeMesh(this.toPayload());
  }

  private readEdgeUses(): EdgeUse[] {
    const map = new Map<string, EdgeUse>();
    this.faces.forEach((face) => {
      for (let index = 0; index < face.length; index += 1) {
        const a = face[index];
        const b = face[(index + 1) % face.length];
        const min = Math.min(a, b);
        const max = Math.max(a, b);
        const key = `${min}:${max}`;
        const current = map.get(key);
        if (current) {
          current.count += 1;
        } else {
          map.set(key, { a: min, b: max, count: 1 });
        }
      }
    });
    return [...map.values()];
  }

  private readUniqueEdges(): EdgeUse[] {
    return this.readEdgeUses();
  }

  private withFaces(faces: number[][], materialIndices: number[]): HalfEdgeMesh {
    return new HalfEdgeMesh({
      faces,
      materialIndices: [...materialIndices],
      vertices: this.vertices
    });
  }
}

function averageVector(first: SceneVector3, second: SceneVector3): SceneVector3 {
  return {
    x: (first.x + second.x) / 2,
    y: (first.y + second.y) / 2,
    z: (first.z + second.z) / 2
  };
}

function cloneVector(vector: SceneVector3): SceneVector3 {
  return { x: vector.x, y: vector.y, z: vector.z };
}

function compactFace(face: number[]): number[] {
  return face.filter((value, index) => index === 0 || value !== face[index - 1]).filter((value, index, values) => index !== values.length - 1 || value !== values[0]);
}

function faceCenter(face: number[], vertices: SceneVector3[]): SceneVector3 {
  const sum = face.reduce(
    (current, vertexIndex) => ({
      x: current.x + vertices[vertexIndex].x,
      y: current.y + vertices[vertexIndex].y,
      z: current.z + vertices[vertexIndex].z
    }),
    { x: 0, y: 0, z: 0 }
  );
  return {
    x: sum.x / face.length,
    y: sum.y / face.length,
    z: sum.z / face.length
  };
}

function faceNormal(face: number[], vertices: SceneVector3[]): SceneVector3 {
  if (face.length < 3) {
    return { x: 0, y: 1, z: 0 };
  }

  const a = vertices[face[0]];
  const b = vertices[face[1]];
  const c = vertices[face[2]];
  const ab = { x: b.x - a.x, y: b.y - a.y, z: b.z - a.z };
  const ac = { x: c.x - a.x, y: c.y - a.y, z: c.z - a.z };
  const normal = {
    x: ab.y * ac.z - ab.z * ac.y,
    y: ab.z * ac.x - ab.x * ac.z,
    z: ab.x * ac.y - ab.y * ac.x
  };
  const length = Math.hypot(normal.x, normal.y, normal.z) || 1;
  return {
    x: normal.x / length,
    y: normal.y / length,
    z: normal.z / length
  };
}

function readFaceVertices(face: SceneEditableMeshPayload['faces'][number] | number[]): number[] {
  return Array.isArray(face) ? [...face] : [...face.vertices];
}

function vectorDistance(first: SceneVector3, second: SceneVector3): number {
  return Math.hypot(first.x - second.x, first.y - second.y, first.z - second.z);
}
