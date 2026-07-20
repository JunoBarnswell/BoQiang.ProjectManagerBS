import DOMPurify from 'dompurify';
import { useMemo } from 'react';

import type { ProjectManagementTaskCommentMention } from '../../../api/project-management/projectManagement.types';

const safeMarkdownUrlPattern = /^(?:https?:|mailto:)/i;
const markdownLinkPattern = /(!?)\[([^\]\r\n]{0,500})\]\(((?:[^()\r\n]|\([^()\r\n]*\))*)\)/g;

export function normalizeProjectManagementMarkdown(value: string): string {
  let markdown = value.replace(/\r\n/g, '\n').replace(/\r/g, '\n').trim();
  if (!markdown) return '';

  markdown = markdown.replace(/<!--[\s\S]*?-->/g, '').replace(/<\/?[A-Za-z][^>]*>/g, '');
  markdown = markdown.replace(markdownLinkPattern, (_match, image: string, label: string, rawDestination: string) => {
    const visibleLabel = label.trim();
    if (!visibleLabel) return '';
    const destination = firstDestinationToken(rawDestination);
    return !image && isSafeMarkdownUrl(destination) ? `[${visibleLabel}](${destination})` : visibleLabel;
  });
  return markdown.replace(/[<>]/g, '').trim();
}

export function sanitizeProjectManagementPaste(plainText: string, htmlText = ''): string {
  const source = plainText || htmlText;
  if (!source) return '';
  const text = DOMPurify.sanitize(source, {
    ALLOW_DATA_ATTR: false,
    ALLOWED_ATTR: [],
    ALLOWED_TAGS: [],
    KEEP_CONTENT: true
  });
  return normalizeProjectManagementMarkdown(text);
}

export function renderProjectManagementMarkdown(value: string, mentions: ProjectManagementTaskCommentMention[] = []): string {
  const markdown = normalizeProjectManagementMarkdown(value);
  if (!markdown) return '';

  const lines = markdown.split('\n');
  const blocks: string[] = [];
  let index = 0;
  while (index < lines.length) {
    const line = lines[index];
    if (!line.trim()) {
      index += 1;
      continue;
    }
    if (line.startsWith('```')) {
      const codeLines: string[] = [];
      index += 1;
      while (index < lines.length && !lines[index].startsWith('```')) {
        codeLines.push(lines[index]);
        index += 1;
      }
      if (index < lines.length) index += 1;
      blocks.push(`<pre><code>${escapeHtml(codeLines.join('\n'))}</code></pre>`);
      continue;
    }
    const heading = /^(#{1,3})\s+(.+)$/.exec(line);
    if (heading) {
      blocks.push(`<h${heading[1].length}>${renderInline(heading[2])}</h${heading[1].length}>`);
      index += 1;
      continue;
    }
    if (/^>\s?/.test(line)) {
      const quoteLines: string[] = [];
      while (index < lines.length && /^>\s?/.test(lines[index])) {
        quoteLines.push(lines[index].replace(/^>\s?/, ''));
        index += 1;
      }
      blocks.push(`<blockquote>${renderInline(quoteLines.join('\n'))}</blockquote>`);
      continue;
    }
    if (/^[-*+]\s+/.test(line) || /^\d+[.)]\s+/.test(line)) {
      const ordered = /^\d+[.)]\s+/.test(line);
      const items: string[] = [];
      while (index < lines.length) {
        const item = ordered ? /^\d+[.)]\s+(.+)$/.exec(lines[index]) : /^[-*+]\s+(.+)$/.exec(lines[index]);
        if (!item) break;
        items.push(`<li>${renderInline(item[1])}</li>`);
        index += 1;
      }
      blocks.push(`<${ordered ? 'ol' : 'ul'}>${items.join('')}</${ordered ? 'ol' : 'ul'}>`);
      continue;
    }

    const paragraph: string[] = [line];
    index += 1;
    while (index < lines.length && lines[index].trim() && !isBlockStart(lines[index])) {
      paragraph.push(lines[index]);
      index += 1;
    }
    blocks.push(`<p>${renderInline(paragraph.join('\n'))}</p>`);
  }

  return sanitizeProjectManagementMarkdownHtml(applyMentionHighlights(blocks.join(''), mentions));
}

export function sanitizeProjectManagementMarkdownHtml(value: string): string {
  return DOMPurify.sanitize(value, {
    ALLOW_DATA_ATTR: true,
    ALLOWED_ATTR: ['class', 'data-type', 'href', 'rel', 'target'],
    ALLOWED_TAGS: ['a', 'blockquote', 'br', 'code', 'em', 'h1', 'h2', 'h3', 'li', 'ol', 'p', 'pre', 'span', 'strong', 'ul'],
    ALLOWED_URI_REGEXP: safeMarkdownUrlPattern,
    FORBID_ATTR: ['style', 'srcdoc'],
    FORBID_TAGS: ['form', 'iframe', 'img', 'object', 'script', 'style', 'svg', 'template']
  });
}

function applyMentionHighlights(html: string, mentions: ProjectManagementTaskCommentMention[]): string {
  if (!mentions.length) return html;
  return mentions.reduce((result, mention) => {
    const label = mention.displayName.trim();
    if (!label) return result;
    const token = `<span class="pm-mention-token" data-type="project-mention">@${escapeHtml(label)}</span>`;
    return result.replace(new RegExp(`@${escapeRegExp(label)}(?![\\w-])`, 'g'), token);
  }, html);
}

export function isSafeMarkdownUrl(value: string): boolean {
  const candidate = value.trim();
  if (!candidate || [...candidate].some((character) => character.charCodeAt(0) <= 0x20)) return false;
  let decoded = candidate;
  try {
    decoded = decodeURIComponent(decoded);
  } catch {
    return false;
  }
  const normalized = decoded.replace(/&(?:#x?[0-9a-f]+|[a-z]+);/gi, '').trim();
  return safeMarkdownUrlPattern.test(normalized);
}

function firstDestinationToken(value: string): string {
  const candidate = value.trim();
  if (candidate.startsWith('<') && candidate.includes('>')) return candidate.slice(1, candidate.indexOf('>')).trim();
  return candidate.split(/[\s\t\n]/, 1)[0] ?? '';
}

function isBlockStart(line: string): boolean {
  return line.startsWith('```') || /^(#{1,3})\s+/.test(line) || /^>\s?/.test(line) || /^[-*+]\s+/.test(line) || /^\d+[.)]\s+/.test(line);
}

function renderInline(value: string): string {
  const tokens: string[] = [];
  let text = escapeHtml(value).replace(/\n/g, '<br>');
  const token = (html: string) => {
    const index = tokens.push(html) - 1;
    return `\uE000${index}\uE001`;
  };
  text = text.replace(/`([^`\n]+)`/g, (_match, code: string) => token(`<code>${code}</code>`));
  text = text.replace(/\[([^\]\n]+)\]\(([^)\s]+)\)/g, (_match, label: string, destination: string) =>
    isSafeMarkdownUrl(destination) ? token(`<a href="${escapeHtml(destination)}" target="_blank" rel="noopener noreferrer">${label}</a>`) : label);
  text = text.replace(/\*\*([^*\n]+)\*\*/g, (_match, content: string) => token(`<strong>${content}</strong>`));
  text = text.replace(/__([^_\n]+)__/g, (_match, content: string) => token(`<strong>${content}</strong>`));
  text = text.replace(/\*([^*\n]+)\*/g, (_match, content: string) => token(`<em>${content}</em>`));
  text = text.replace(/_([^_\n]+)_/g, (_match, content: string) => token(`<em>${content}</em>`));
  return text.replace(/\uE000(\d+)\uE001/g, (_match, index: string) => tokens[Number(index)] ?? '');
}

function escapeHtml(value: string): string {
  return value.replace(/[&<>"']/g, (character) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[character] ?? character);
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

export function ProjectManagementMarkdownContent({
  className,
  mentions = [],
  value,
}: {
  className?: string;
  mentions?: ProjectManagementTaskCommentMention[];
  value: string;
}) {
  const html = useMemo(() => renderProjectManagementMarkdown(value, mentions), [mentions, value]);
  return <div className={className} dangerouslySetInnerHTML={{ __html: html }} />;
}
