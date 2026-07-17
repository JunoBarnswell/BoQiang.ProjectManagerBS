const MAX_SCAN_DEPTH = 128;

/**
 * Finds duplicate object keys before JSON.parse can silently overwrite them.
 * Syntax validity remains the responsibility of JSON.parse; this scanner only
 * reports duplicates in syntactically traversable JSON input.
 */
export function findDuplicateJsonKeys(source: string): string[] {
  const duplicates: string[] = [];
  let index = 0;

  try {
    readValue('$', 0);
  } catch {
    return [];
  }

  return [...new Set(duplicates)];

  function readValue(path: string, depth: number): void {
    if (depth > MAX_SCAN_DEPTH) return;
    skipWhitespace();
    const token = source[index];
    if (token === '{') {
      readObject(path, depth + 1);
      return;
    }
    if (token === '[') {
      readArray(path, depth + 1);
      return;
    }
    if (token === '"') {
      readStringEnd();
      return;
    }
    readPrimitive();
  }

  function readObject(path: string, depth: number): void {
    index += 1;
    skipWhitespace();
    if (source[index] === '}') {
      index += 1;
      return;
    }

    const keys = new Set<string>();
    while (index < source.length) {
      skipWhitespace();
      if (source[index] !== '"') throw new Error('object key expected');
      const start = index;
      readStringEnd();
      const key = JSON.parse(source.slice(start, index)) as string;
      if (keys.has(key)) duplicates.push(`${path}.${JSON.stringify(key)}`);
      keys.add(key);
      skipWhitespace();
      if (source[index] !== ':') throw new Error('object separator expected');
      index += 1;
      readValue(`${path}.${JSON.stringify(key)}`, depth);
      skipWhitespace();
      if (source[index] === ',') {
        index += 1;
        continue;
      }
      if (source[index] === '}') {
        index += 1;
        return;
      }
      throw new Error('object terminator expected');
    }
    throw new Error('unterminated object');
  }

  function readArray(path: string, depth: number): void {
    index += 1;
    skipWhitespace();
    if (source[index] === ']') {
      index += 1;
      return;
    }

    let itemIndex = 0;
    while (index < source.length) {
      readValue(`${path}[${itemIndex}]`, depth);
      itemIndex += 1;
      skipWhitespace();
      if (source[index] === ',') {
        index += 1;
        continue;
      }
      if (source[index] === ']') {
        index += 1;
        return;
      }
      throw new Error('array terminator expected');
    }
    throw new Error('unterminated array');
  }

  function readStringEnd(): void {
    if (source[index] !== '"') throw new Error('string expected');
    index += 1;
    while (index < source.length) {
      const character = source[index];
      if (character === '\\') {
        index += 2;
        continue;
      }
      index += 1;
      if (character === '"') return;
      if (character === '\n' || character === '\r') throw new Error('invalid string');
    }
    throw new Error('unterminated string');
  }

  function readPrimitive(): void {
    while (index < source.length && !/[,\]}\s]/.test(source[index])) index += 1;
  }

  function skipWhitespace(): void {
    while (index < source.length && /\s/.test(source[index])) index += 1;
  }
}
