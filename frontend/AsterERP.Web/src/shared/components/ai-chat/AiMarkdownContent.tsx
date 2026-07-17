import { Check, Copy } from 'lucide-react';
import { useState, type ReactNode } from 'react';

import { useI18n } from '../../../core/i18n/I18nProvider';

interface AiMarkdownContentProps {
  content: string;
}

interface Segment {
  code?: string;
  language?: string;
  text?: string;
  type: 'code' | 'text';
}

export function AiMarkdownContent({ content }: AiMarkdownContentProps) {
  const { translate } = useI18n();
  const segments = splitMarkdown(content);

  return (
    <div className="ai-markdown-content">
      {segments.map((segment, index) =>
        segment.type === 'code' ? (
          <CodeBlock code={segment.code ?? ''} key={`${index}-code`} language={segment.language} translate={translate} />
        ) : (
          <TextBlock key={`${index}-text`} text={segment.text ?? ''} />
        )
      )}
    </div>
  );
}

function splitMarkdown(content: string): Segment[] {
  const segments: Segment[] = [];
  const fencePattern = /```([a-zA-Z0-9_-]+)?\n([\s\S]*?)```/g;
  let cursor = 0;
  let match: RegExpExecArray | null;

  while ((match = fencePattern.exec(content)) !== null) {
    if (match.index > cursor) {
      segments.push({ text: content.slice(cursor, match.index), type: 'text' });
    }

    segments.push({
      code: match[2],
      language: match[1],
      type: 'code'
    });
    cursor = match.index + match[0].length;
  }

  if (cursor < content.length) {
    segments.push({ text: content.slice(cursor), type: 'text' });
  }

  return segments.length > 0 ? segments : [{ text: content, type: 'text' }];
}

function TextBlock({ text }: { text: string }) {
  const lines = text.split('\n');
  const nodes: ReactNode[] = [];
  let index = 0;

  while (index < lines.length) {
    const line = lines[index] ?? '';
    const nextLine = lines[index + 1] ?? '';

    if (!line.trim()) {
      index += 1;
      continue;
    }

    if (isTableHeader(line, nextLine)) {
      const tableLines = [line, nextLine];
      index += 2;
      while (index < lines.length && lines[index]?.includes('|')) {
        tableLines.push(lines[index] ?? '');
        index += 1;
      }
      nodes.push(<MarkdownTable key={`table-${index}`} lines={tableLines} />);
      continue;
    }

    if (/^#{1,4}\s+/.test(line)) {
      const level = Math.min(line.match(/^#+/)?.[0].length ?? 2, 4);
      const body = line.replace(/^#{1,4}\s+/, '');
      nodes.push(
        <MarkdownHeading key={`heading-${index}`} level={level}>
          {renderInline(body)}
        </MarkdownHeading>
      );
      index += 1;
      continue;
    }

    if (/^\s*[-*]\s+/.test(line)) {
      const items: string[] = [];
      while (index < lines.length && /^\s*[-*]\s+/.test(lines[index] ?? '')) {
        items.push((lines[index] ?? '').replace(/^\s*[-*]\s+/, ''));
        index += 1;
      }
      nodes.push(
        <ul key={`list-${index}`}>
          {items.map((item, itemIndex) => (
            <li key={`${itemIndex}-${item}`}>{renderInline(item)}</li>
          ))}
        </ul>
      );
      continue;
    }

    if (/^\s*\d+[.)]\s+/.test(line)) {
      const items: string[] = [];
      while (index < lines.length && /^\s*\d+[.)]\s+/.test(lines[index] ?? '')) {
        items.push((lines[index] ?? '').replace(/^\s*\d+[.)]\s+/, ''));
        index += 1;
      }
      nodes.push(
        <ol key={`ordered-list-${index}`}>
          {items.map((item, itemIndex) => (
            <li key={`${itemIndex}-${item}`}>{renderInline(item)}</li>
          ))}
        </ol>
      );
      continue;
    }

    const paragraphLines = [line];
    index += 1;
    while (
      index < lines.length &&
      lines[index]?.trim() &&
      !isTableHeader(lines[index] ?? '', lines[index + 1] ?? '') &&
      !/^#{1,4}\s+/.test(lines[index] ?? '') &&
      !/^\s*[-*]\s+/.test(lines[index] ?? '') &&
      !/^\s*\d+[.)]\s+/.test(lines[index] ?? '')
    ) {
      paragraphLines.push(lines[index] ?? '');
      index += 1;
    }
    nodes.push(<p key={`p-${index}`}>{renderInline(paragraphLines.join(' '))}</p>);
  }

  return <>{nodes}</>;
}

function MarkdownHeading({ children, level }: { children: ReactNode; level: number }) {
  if (level === 1) return <h1>{children}</h1>;
  if (level === 2) return <h2>{children}</h2>;
  if (level === 3) return <h3>{children}</h3>;
  return <h4>{children}</h4>;
}

function CodeBlock({ code, language, translate }: { code: string; language?: string; translate: (key: string) => string }) {
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    await navigator.clipboard.writeText(code);
    setCopied(true);
    window.setTimeout(() => setCopied(false), 1200);
  };

  return (
    <figure className="ai-code-block">
      <figcaption>
        <span>{language || translate('ai.markdown.language.text')}</span>
        <button aria-label={translate('ai.markdown.copyCode')} type="button" onClick={() => void handleCopy()}>
          {copied ? <Check size={14} /> : <Copy size={14} />}
        </button>
      </figcaption>
      <pre>
        <code>{code}</code>
      </pre>
    </figure>
  );
}

function MarkdownTable({ lines }: { lines: string[] }) {
  const header = parseTableCells(lines[0] ?? '');
  const rows = lines.slice(2).map(parseTableCells);

  return (
    <div className="ai-markdown-table-wrap">
      <table>
        <thead>
          <tr>
            {header.map((cell) => (
              <th key={cell}>{renderInline(cell)}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, rowIndex) => (
            <tr key={`${rowIndex}-${row.join('|')}`}>
              {header.map((_, cellIndex) => (
                <td key={cellIndex}>{renderInline(row[cellIndex] ?? '')}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function isTableHeader(line: string, nextLine: string): boolean {
  return line.includes('|') && /^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?\s*$/.test(nextLine);
}

function parseTableCells(line: string): string[] {
  return line
    .trim()
    .replace(/^\|/, '')
    .replace(/\|$/, '')
    .split('|')
    .map((cell) => cell.trim());
}

function renderInline(text: string): ReactNode[] {
  const parts = text.split(/(`[^`]+`)/g);
  return parts.map((part, index) => {
    if (part.startsWith('`') && part.endsWith('`')) {
      return <code key={`${index}-${part}`}>{part.slice(1, -1)}</code>;
    }

    return <span key={`${index}-${part}`}>{part}</span>;
  });
}
