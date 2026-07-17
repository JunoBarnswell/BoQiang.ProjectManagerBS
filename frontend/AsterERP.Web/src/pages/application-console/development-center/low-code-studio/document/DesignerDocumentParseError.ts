export class DesignerDocumentParseError extends Error {
  public readonly source: string | null;
  public readonly diagnostics: readonly string[];

  public constructor(source: string | null, diagnostics: readonly string[]) {
    super(`Designer document is invalid: ${diagnostics.join('; ')}`);
    this.name = 'DesignerDocumentParseError';
    this.source = source;
    this.diagnostics = diagnostics;
  }
}
