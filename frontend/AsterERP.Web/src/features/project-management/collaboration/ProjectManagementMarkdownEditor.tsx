import { useRef, useState } from 'react';

import {
  normalizeProjectManagementMarkdown,
  ProjectManagementMarkdownContent,
  sanitizeProjectManagementPaste
} from './projectManagementMarkdown';

interface ProjectManagementMarkdownEditorProps {
  ariaLabel: string;
  onChange: (value: string) => void;
  placeholder?: string;
  rows?: number;
  value: string;
}

export function ProjectManagementMarkdownEditor({ ariaLabel, onChange, placeholder, rows = 5, value }: ProjectManagementMarkdownEditorProps) {
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const [preview, setPreview] = useState(false);

  const replaceSelection = (before: string, after = before) => {
    const textarea = textareaRef.current;
    if (!textarea) return;
    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const selected = value.slice(start, end);
    const next = `${value.slice(0, start)}${before}${selected || '文本'}${after}${value.slice(end)}`;
    onChange(normalizeProjectManagementMarkdown(next));
    requestAnimationFrame(() => {
      textarea.focus();
      const cursor = start + before.length + (selected || '文本').length + after.length;
      textarea.setSelectionRange(cursor, cursor);
    });
  };

  const prefixLine = (prefix: string) => {
    const textarea = textareaRef.current;
    if (!textarea) return;
    const start = textarea.selectionStart;
    const lineStart = value.lastIndexOf('\n', Math.max(0, start - 1)) + 1;
    const next = `${value.slice(0, lineStart)}${prefix}${value.slice(lineStart)}`;
    onChange(normalizeProjectManagementMarkdown(next));
    requestAnimationFrame(() => {
      textarea.focus();
      textarea.setSelectionRange(start + prefix.length, start + prefix.length);
    });
  };

  return <div className="rounded border border-gray-200">
    <div className="flex flex-wrap items-center gap-1 border-b border-gray-100 p-1 text-xs">
      <button type="button" onClick={() => replaceSelection('**')} aria-label="加粗">粗体</button>
      <button type="button" onClick={() => replaceSelection('*')} aria-label="斜体">斜体</button>
      <button type="button" onClick={() => replaceSelection('`')} aria-label="行内代码">代码</button>
      <button type="button" onClick={() => replaceSelection('[', '](https://example.com)')} aria-label="插入安全链接">链接</button>
      <button type="button" onClick={() => prefixLine('# ')} aria-label="插入标题">标题</button>
      <button type="button" onClick={() => prefixLine('- ')} aria-label="插入列表">列表</button>
      <button type="button" onClick={() => prefixLine('> ')} aria-label="插入引用">引用</button>
      <button className="ml-auto" type="button" onClick={() => setPreview((current) => !current)}>{preview ? '编辑' : '预览'}</button>
    </div>
    {preview ? <ProjectManagementMarkdownContent className="min-h-24 p-2 text-sm" value={value} /> : <textarea
      ref={textareaRef}
      aria-label={ariaLabel}
      className="min-h-24 w-full resize-y border-0 p-2 outline-none"
      onChange={(event) => onChange(normalizeProjectManagementMarkdown(event.target.value))}
      onPaste={(event) => {
        const plainText = event.clipboardData.getData('text/plain');
        const htmlText = event.clipboardData.getData('text/html');
        if (!htmlText && !plainText) return;
        event.preventDefault();
        const textarea = textareaRef.current;
        if (!textarea) return;
        const start = textarea.selectionStart;
        const end = textarea.selectionEnd;
        const pasted = sanitizeProjectManagementPaste(plainText, htmlText);
        onChange(normalizeProjectManagementMarkdown(`${value.slice(0, start)}${pasted}${value.slice(end)}`));
      }}
      placeholder={placeholder}
      rows={rows}
      value={value}
    />}
  </div>;
}
