const browserNotSupportedError = new Error('fs/promises is not available in browser runtime');

function throwBrowserNotSupported(): never {
  throw browserNotSupportedError;
}

export const readFile = async (...args: unknown[]): Promise<Uint8Array> => {
  void args;
  return throwBrowserNotSupported();
};

export const writeFile = async (...args: unknown[]): Promise<void> => {
  void args;
  return throwBrowserNotSupported();
};

export default {
  readFile,
  writeFile
};
