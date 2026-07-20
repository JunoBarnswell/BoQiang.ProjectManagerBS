import { Extension, mergeAttributes, Node as TiptapNode } from '@tiptap/core';
import Suggestion, { type SuggestionProps } from '@tiptap/suggestion';
import { useEditor, EditorContent } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import Link from '@tiptap/extension-link';
import Placeholder from '@tiptap/extension-placeholder';
import { useEffect, useMemo, useRef } from 'react';

import type { ProjectManagementMemberCandidate } from '../../../api/project-management/projectManagement.types';
import { normalizeProjectManagementMarkdown, ProjectManagementMarkdownContent } from './projectManagementMarkdown';

interface ProjectManagementMarkdownEditorProps {
  ariaLabel: string;
  onChange: (value: string) => void;
  placeholder?: string;
  rows?: number;
  value: string;
  contentJson?: string;
  mentionCandidates?: ProjectManagementMemberCandidate[];
  onContentJsonChange?: (value: string) => void;
  onMentionUserIdsChange?: (value: string[]) => void;
}

interface MentionItem {
  id: string;
  label: string;
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

function createProjectMentionSuggestion(candidatesRef: { current: ProjectManagementMemberCandidate[] }, onMentionIdsChangeRef: { current?: (value: string[]) => void }) {
  return Extension.create({
    name: 'projectMentionSuggestion',
    addProseMirrorPlugins() {
      return [Suggestion<MentionItem>({
        editor: this.editor,
        char: '@',
        allowSpaces: true,
        items: ({ query }) => candidatesRef.current
          .filter((candidate) => candidate.isSelectable && `${candidate.displayName} ${candidate.userName}`.toLowerCase().includes(query.trim().toLowerCase()))
          .slice(0, 8)
          .map((candidate) => ({ id: candidate.userId, label: candidate.displayName || candidate.userName })),
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
              loading.textContent = '搜索成员…';
              currentPopup.appendChild(loading);
              return;
            }
            if (!props.items.length) {
              const empty = document.createElement('div');
              empty.className = 'pm-mention-suggestion__empty';
              empty.textContent = '没有匹配的项目成员';
              currentPopup.appendChild(empty);
              return;
            }
            selectedIndex = Math.min(selectedIndex, props.items.length - 1);
            props.items.forEach((item, index) => {
              const button = document.createElement('button');
              button.type = 'button';
              button.className = `pm-mention-suggestion__item${index === selectedIndex ? ' is-active' : ''}`;
              button.textContent = `@${item.label}`;
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

export function ProjectManagementMarkdownEditor({ ariaLabel, contentJson, mentionCandidates = [], onChange, onContentJsonChange, onMentionUserIdsChange, placeholder, value }: ProjectManagementMarkdownEditorProps) {
  const candidatesRef = useRef(mentionCandidates);
  const onChangeRef = useRef(onChange);
  const onContentJsonChangeRef = useRef(onContentJsonChange);
  const onMentionIdsChangeRef = useRef(onMentionUserIdsChange);
  candidatesRef.current = mentionCandidates;
  onChangeRef.current = onChange;
  onContentJsonChangeRef.current = onContentJsonChange;
  onMentionIdsChangeRef.current = onMentionUserIdsChange;
  const extensions = useMemo(() => [
    StarterKit,
    Link.configure({ openOnClick: false, autolink: true, HTMLAttributes: { rel: 'noopener noreferrer', target: '_blank' } }),
    Placeholder.configure({ placeholder: placeholder ?? '请输入内容' }),
    ProjectMentionNode,
    createProjectMentionSuggestion(candidatesRef, onMentionIdsChangeRef),
  ], []);
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
    const current = htmlToMarkdown(editor.getHTML());
    const desiredJson = parseEditorJson(contentJson);
    const currentJson = JSON.stringify(editor.getJSON());
    if (desiredJson && currentJson !== JSON.stringify(desiredJson)) {
      editor.commands.setContent(desiredJson, { emitUpdate: false });
    } else if (normalizeProjectManagementMarkdown(current) !== normalizeProjectManagementMarkdown(value)) {
      editor.commands.setContent(markdownToHtml(value), { emitUpdate: false });
    }
  }, [contentJson, editor, value]);

  if (!editor) return <div aria-label={ariaLabel} className="min-h-24 rounded border border-gray-200 p-3 text-sm text-gray-500">正在加载编辑器…</div>;
  return <div className="pm-rich-editor rounded border border-gray-200">
    <div aria-label="富文本工具栏" className="pm-rich-editor-toolbar">
      <button type="button" aria-label="段落" onClick={() => editor.chain().focus().setParagraph().run()}>段落</button>
      <button type="button" aria-label="标题" onClick={() => editor.chain().focus().toggleHeading({ level: 2 }).run()}>标题</button>
      <button type="button" aria-label="加粗" onClick={() => editor.chain().focus().toggleBold().run()}>粗体</button>
      <button type="button" aria-label="斜体" onClick={() => editor.chain().focus().toggleItalic().run()}>斜体</button>
      <button type="button" aria-label="删除线" onClick={() => editor.chain().focus().toggleStrike().run()}>删除线</button>
      <button type="button" aria-label="无序列表" onClick={() => editor.chain().focus().toggleBulletList().run()}>列表</button>
      <button type="button" aria-label="有序列表" onClick={() => editor.chain().focus().toggleOrderedList().run()}>编号</button>
      <button type="button" aria-label="引用" onClick={() => editor.chain().focus().toggleBlockquote().run()}>引用</button>
      <button type="button" aria-label="插入链接" onClick={() => { const href = window.prompt('链接地址'); if (href) editor.chain().focus().setLink({ href }).run(); }}>链接</button>
      <button type="button" aria-label="提及成员" onClick={() => editor.chain().focus().insertContent('@').run()}>@成员</button>
      <button type="button" aria-label="撤销" onClick={() => editor.chain().focus().undo().run()}>撤销</button>
      <button type="button" aria-label="重做" onClick={() => editor.chain().focus().redo().run()}>重做</button>
    </div>
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
