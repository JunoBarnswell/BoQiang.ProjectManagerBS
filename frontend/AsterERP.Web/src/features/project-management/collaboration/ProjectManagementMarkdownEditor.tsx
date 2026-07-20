import { Extension, mergeAttributes, Node as TiptapNode } from '@tiptap/core';
import Link from '@tiptap/extension-link';
import Placeholder from '@tiptap/extension-placeholder';
import { useEditor, EditorContent } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import Suggestion, { type SuggestionProps } from '@tiptap/suggestion';
import { useEffect, useMemo, useRef } from 'react';

import type { ProjectManagementMemberCandidate } from '../../../api/project-management/projectManagement.types';
import { useProjectManagementI18n } from '../projectManagementI18n';

import { normalizeProjectManagementMarkdown, ProjectManagementMarkdownContent } from './projectManagementMarkdown';

interface ProjectManagementMarkdownEditorProps {
  ariaLabel: string;
  density?: 'compact' | 'default';
  onChange: (value: string) => void;
  placeholder?: string;
  rows?: number;
  showToolbar?: boolean;
  value: string;
  contentJson?: string;
  mentionCandidates?: ProjectManagementMemberCandidate[];
  onMentionSearch?: (keyword: string) => Promise<ProjectManagementMemberCandidate[]>;
  onContentJsonChange?: (value: string) => void;
  onMentionUserIdsChange?: (value: string[]) => void;
}

interface MentionItem {
  id: string;
  label: string;
  subtitle?: string;
}

const ProjectMentionNode = TiptapNode.create({
  name: 'projectMention',
  group: 'inline',
  inline: true,
  atom: true,
  selectable: false,
  addAttributes: () => ({
    id: { default: '' },
    label: { default: '' },
  }),
  parseHTML: () => [{ tag: 'span[data-type="project-mention"]' }],
  renderHTML: ({ HTMLAttributes }) => [
    'span',
    mergeAttributes(HTMLAttributes, { 'data-type': 'project-mention', class: 'pm-mention-token' }),
    `@${HTMLAttributes.label ?? ''}`,
  ],
});

function createProjectMentionSuggestion(candidatesRef: { current: ProjectManagementMemberCandidate[] }, onMentionIdsChangeRef: { current?: (value: string[]) => void }, textsRef: { current: Record<string, string> }, searchRef: { current?: (keyword: string) => Promise<ProjectManagementMemberCandidate[]> }) {
  return Extension.create({
    name: 'projectMentionSuggestion',
    addProseMirrorPlugins() {
      return [Suggestion<MentionItem>({
        editor: this.editor,
        char: '@',
        allowSpaces: true,
        items: async ({ query }) => {
          const normalized = query.trim();
          const local = candidatesRef.current
            .filter((candidate) => candidate.isSelectable && `${candidate.displayName} ${candidate.userName} ${candidate.deptName ?? ''}`.toLowerCase().includes(normalized.toLowerCase()));
          const remote = searchRef.current
            ? await debounceMentionSearch(searchRef.current, normalized)
            : [];
          const byId = new Map<string, ProjectManagementMemberCandidate>();
          [...local, ...remote].forEach((candidate) => {
            if (candidate.isSelectable && !byId.has(candidate.userId)) byId.set(candidate.userId, candidate);
          });
          return [...byId.values()].slice(0, 10).map((candidate) => ({
            id: candidate.userId,
            label: candidate.displayName || candidate.userName,
            subtitle: [candidate.deptName, candidate.positionName].filter(Boolean).join(' · '),
          }));
        },
        command: ({ editor, range, props }) => {
          editor.chain().focus().insertContentAt(range, {
            type: 'projectMention',
            attrs: props,
          }).run();
          onMentionIdsChangeRef.current?.(collectMentionIds(editor.getJSON()));
        },
        render: () => {
          let popup: HTMLDivElement | undefined;
          let unmount: (() => void) | undefined;
          let activeProps: SuggestionProps<MentionItem> | undefined;
          let selectedIndex = 0;

          const renderPopup = (props: SuggestionProps<MentionItem>) => {
            activeProps = props;
            const currentPopup = popup;
            if (!currentPopup) return;
            currentPopup.replaceChildren();
            if (props.loading) {
              const loading = document.createElement('div');
              loading.className = 'pm-mention-suggestion__empty';
              loading.textContent = textsRef.current.searchMembers;
              currentPopup.appendChild(loading);
              return;
            }
            if (!props.items.length) {
              const empty = document.createElement('div');
              empty.className = 'pm-mention-suggestion__empty';
              empty.textContent = textsRef.current.noMembers;
              currentPopup.appendChild(empty);
              return;
            }
            selectedIndex = Math.min(selectedIndex, props.items.length - 1);
            props.items.forEach((item, index) => {
              const button = document.createElement('button');
              button.type = 'button';
              button.className = `pm-mention-suggestion__item${index === selectedIndex ? ' is-active' : ''}`;
              button.textContent = item.subtitle ? `@${item.label} · ${item.subtitle}` : `@${item.label}`;
              button.addEventListener('mousedown', (event) => {
                event.preventDefault();
                props.command(item);
              });
              currentPopup.appendChild(button);
            });
          };

          return {
            onStart: (props) => {
              popup = document.createElement('div');
              popup.className = 'pm-mention-suggestion';
              unmount = props.mount(popup);
              renderPopup(props);
            },
            onUpdate: renderPopup,
            onKeyDown: ({ event }) => {
              if (!activeProps?.items.length) return false;
              if (event.key === 'ArrowDown') {
                event.preventDefault();
                selectedIndex = (selectedIndex + 1) % activeProps.items.length;
                renderPopup(activeProps);
                return true;
              }
              if (event.key === 'ArrowUp') {
                event.preventDefault();
                selectedIndex = (selectedIndex - 1 + activeProps.items.length) % activeProps.items.length;
                renderPopup(activeProps);
                return true;
              }
              if (event.key === 'Enter' || event.key === 'Tab') {
                event.preventDefault();
                activeProps.command(activeProps.items[selectedIndex]);
                return true;
              }
              return false;
            },
            onExit: () => {
              unmount?.();
              unmount = undefined;
              popup = undefined;
              activeProps = undefined;
            },
          };
        },
      })];
    },
  });
}

function debounceMentionSearch(
  search: (keyword: string) => Promise<ProjectManagementMemberCandidate[]>,
  keyword: string,
): Promise<ProjectManagementMemberCandidate[]> {
  return new Promise((resolve, reject) => {
    window.setTimeout(() => {
      void search(keyword).then(resolve, reject);
    }, 180);
  });
}

function resolveEditorDensity(density: ProjectManagementMarkdownEditorProps['density'], rows?: number): 'compact' | 'default' {
  if (density) return density;
  if (rows !== undefined && rows <= 3) return 'compact';
  return 'default';
}

export function ProjectManagementMarkdownEditor({ ariaLabel, contentJson, density, mentionCandidates = [], onChange, onContentJsonChange, onMentionSearch, onMentionUserIdsChange, placeholder, rows, showToolbar = true, value }: ProjectManagementMarkdownEditorProps) {
  const editorDensity = resolveEditorDensity(density, rows);
  const { t } = useProjectManagementI18n();
  const candidatesRef = useRef(mentionCandidates);
  const onChangeRef = useRef(onChange);
  const onContentJsonChangeRef = useRef(onContentJsonChange);
  const onMentionIdsChangeRef = useRef(onMentionUserIdsChange);
  const mentionSearchRef = useRef(onMentionSearch);
  const textsRef = useRef<Record<string, string>>({ searchMembers: '', noMembers: '' });
  candidatesRef.current = mentionCandidates;
  onChangeRef.current = onChange;
  onContentJsonChangeRef.current = onContentJsonChange;
  onMentionIdsChangeRef.current = onMentionUserIdsChange;
  mentionSearchRef.current = onMentionSearch;
  textsRef.current = { searchMembers: t('projectManagement.richEditor.searchMembers'), noMembers: t('projectManagement.richEditor.noMembers') };
  const extensions = useMemo(() => [
    StarterKit,
    Link.configure({ openOnClick: false, autolink: true, HTMLAttributes: { rel: 'noopener noreferrer', target: '_blank' } }),
    Placeholder.configure({ placeholder: placeholder ?? t('projectManagement.richEditor.placeholder') }),
    ProjectMentionNode,
    createProjectMentionSuggestion(candidatesRef, onMentionIdsChangeRef, textsRef, mentionSearchRef),
  ], [placeholder, t]);
  const editor = useEditor({
    immediatelyRender: false,
    extensions,
    content: parseEditorJson(contentJson) ?? markdownToHtml(value),
    editorProps: { attributes: { 'aria-label': ariaLabel, class: 'pm-tiptap-content' } },
    onUpdate: ({ editor: current }) => {
      onChangeRef.current(htmlToMarkdown(current.getHTML()));
      onContentJsonChangeRef.current?.(JSON.stringify(current.getJSON()));
      onMentionIdsChangeRef.current?.(collectMentionIds(current.getJSON()));
    },
  });

  useEffect(() => {
    if (!editor) return;
    const desiredJson = parseEditorJson(contentJson);
    const currentJson = JSON.stringify(editor.getJSON());
    if (desiredJson && currentJson !== JSON.stringify(desiredJson)) {
      editor.commands.setContent(desiredJson, { emitUpdate: false });
      return;
    }
    const current = htmlToMarkdown(editor.getHTML());
    if (normalizeProjectManagementMarkdown(current) !== normalizeProjectManagementMarkdown(value)) {
      editor.commands.setContent(markdownToHtml(value), { emitUpdate: false });
    }
  }, [contentJson, editor, value]);

  if (!editor) return <div aria-label={ariaLabel} className="min-h-24 rounded border border-gray-200 p-3 text-sm text-gray-500">{t('projectManagement.richEditor.loading')}</div>;
  return <div className={`pm-rich-editor pm-rich-editor--${editorDensity}${showToolbar ? '' : ' pm-rich-editor--toolbarless'}`}>
    {showToolbar ? <div aria-label={t('projectManagement.richEditor.toolbar')} className="pm-rich-editor-toolbar">
      <button type="button" aria-label={t('projectManagement.richEditor.paragraph')} onClick={() => editor.chain().focus().setParagraph().run()}>{t('projectManagement.richEditor.paragraph')}</button>
      <button type="button" aria-label={t('projectManagement.richEditor.heading')} onClick={() => editor.chain().focus().toggleHeading({ level: 2 }).run()}>{t('projectManagement.richEditor.heading')}</button>
      <button type="button" aria-label={t('projectManagement.richEditor.bold')} onClick={() => editor.chain().focus().toggleBold().run()}>{t('projectManagement.richEditor.bold')}</button>
      <button type="button" aria-label={t('projectManagement.richEditor.italic')} onClick={() => editor.chain().focus().toggleItalic().run()}>{t('projectManagement.richEditor.italic')}</button>
      <button type="button" aria-label={t('projectManagement.richEditor.strike')} onClick={() => editor.chain().focus().toggleStrike().run()}>{t('projectManagement.richEditor.strike')}</button>
      <button type="button" aria-label={t('projectManagement.richEditor.bulletList')} onClick={() => editor.chain().focus().toggleBulletList().run()}>{t('projectManagement.richEditor.bulletList')}</button>
      <button type="button" aria-label={t('projectManagement.richEditor.orderedList')} onClick={() => editor.chain().focus().toggleOrderedList().run()}>{t('projectManagement.richEditor.orderedList')}</button>
      <button type="button" aria-label={t('projectManagement.richEditor.quote')} onClick={() => editor.chain().focus().toggleBlockquote().run()}>{t('projectManagement.richEditor.quote')}</button>
      <button type="button" aria-label={t('projectManagement.richEditor.link')} onClick={() => { const href = window.prompt(t('projectManagement.richEditor.linkPrompt')); if (href) editor.chain().focus().setLink({ href }).run(); }}>{t('projectManagement.richEditor.link')}</button>
      <button type="button" aria-label={t('projectManagement.richEditor.mention')} onClick={() => editor.chain().focus().insertContent('@').run()}>{t('projectManagement.richEditor.mention')}</button>
      <button type="button" aria-label={t('projectManagement.richEditor.undo')} onClick={() => editor.chain().focus().undo().run()}>{t('projectManagement.richEditor.undo')}</button>
      <button type="button" aria-label={t('projectManagement.richEditor.redo')} onClick={() => editor.chain().focus().redo().run()}>{t('projectManagement.richEditor.redo')}</button>
    </div> : null}
    <EditorContent editor={editor} className="pm-rich-editor-content" />
  </div>;
}

function markdownToHtml(value: string): string {
  const markdown = normalizeProjectManagementMarkdown(value);
  if (!markdown) return '';
  return markdown.split('\n').map(line => {
    if (/^#{1,3}\s+/.test(line)) { const match = /^(#{1,3})\s+(.+)$/.exec(line)!; return `<h${match[1].length}>${inlineMarkdownToHtml(match[2])}</h${match[1].length}>`; }
    if (/^[-*+]\s+/.test(line)) return `<ul><li>${inlineMarkdownToHtml(line.replace(/^[-*+]\s+/, ''))}</li></ul>`;
    if (/^\d+[.)]\s+/.test(line)) return `<ol><li>${inlineMarkdownToHtml(line.replace(/^\d+[.)]\s+/, ''))}</li></ol>`;
    return `<p>${inlineMarkdownToHtml(line)}</p>`;
  }).join('');
}

function inlineMarkdownToHtml(value: string): string {
  return escapeHtml(value)
    .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
    .replace(/__([^_]+)__/g, '<strong>$1</strong>')
    .replace(/\*([^*]+)\*/g, '<em>$1</em>')
    .replace(/_([^_]+)_/g, '<em>$1</em>')
    .replace(/\[([^\]]+)\]\((https?:[^)]+)\)/g, '<a href="$2">$1</a>');
}

function htmlToMarkdown(value: string): string {
  if (typeof DOMParser === 'undefined') return value;
  const document = new DOMParser().parseFromString(value, 'text/html');
  const render = (node: Node): string => {
    if (node.nodeType === Node.TEXT_NODE) return node.textContent ?? '';
    if (!(node instanceof HTMLElement)) return Array.from(node.childNodes).map(render).join('');
    if (node.dataset.type === 'project-mention') {
      const label = node.dataset.label ?? (node.textContent ?? '').replace(/^@/, '');
      return `@${label}`;
    }
    const content = Array.from(node.childNodes).map(render).join('');
    switch (node.tagName.toLowerCase()) {
      case 'strong': case 'b': return `**${content}**`;
      case 'em': case 'i': return `*${content}*`;
      case 's': case 'del': return `~~${content}~~`;
      case 'h1': return `# ${content}\n`;
      case 'h2': return `## ${content}\n`;
      case 'h3': return `### ${content}\n`;
      case 'li': return `- ${content}\n`;
      case 'a': return `[${content}](${node.getAttribute('href') ?? ''})`;
      case 'p': case 'blockquote': case 'ul': case 'ol': return `${content}\n`;
      case 'br': return '\n';
      default: return content;
    }
  };
  return normalizeProjectManagementMarkdown(render(document.body));
}

function escapeHtml(value: string): string { return value.replace(/[&<>"']/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[character] ?? character); }

function parseEditorJson(value?: string): Record<string, unknown> | undefined {
  if (!value?.trim()) return undefined;
  try {
    const parsed = JSON.parse(value) as unknown;
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed as Record<string, unknown> : undefined;
  } catch {
    return undefined;
  }
}

function collectMentionIds(value: unknown): string[] {
  const ids: string[] = [];
  const visit = (node: unknown) => {
    if (!node || typeof node !== 'object') return;
    const record = node as Record<string, unknown>;
    if (record.type === 'projectMention' && record.attrs && typeof record.attrs === 'object') {
      const id = (record.attrs as Record<string, unknown>).id;
      if (typeof id === 'string' && id.trim()) ids.push(id);
    }
    const content = record.content;
    if (Array.isArray(content)) content.forEach(visit);
  };
  visit(value);
  return [...new Set(ids)];
}

export { ProjectManagementMarkdownContent };
