import { createElement } from 'react';
import type { ReactNode } from 'react';

import { resolveSafeRuntimeUrl } from '../shared/runtime/designer-document/designerRuntimeSecurityPolicy';

import type { RuntimeComponentRenderContext } from './RuntimeComponentTypes';
import { applyRuntimeNodePresentation } from './RuntimeNodePresentation';

const standardTags: Readonly<Record<string, string>> = {
  'interaction.details': 'details',
  'interaction.dialog': 'dialog',
  'interaction.popover': 'div',
  'layout.column': 'section',
  'layout.form': 'form',
  'layout.formItem': 'label',
  'layout.html': 'div',
  'layout.page': 'main',
  'layout.print': 'section',
  'layout.responsive': 'section',
  'layout.row': 'section',
  'layout.split': 'section',
  'layout.tableContainer': 'section',
  'layout.tabs': 'section',
  'layout.template': 'section',
  'list.dd': 'dd',
  'list.dl': 'dl',
  'list.dt': 'dt',
  'list.li': 'li',
  'list.menu': 'menu',
  'list.ol': 'ol',
  'list.ul': 'ul',
  'media.audio': 'audio',
  'media.canvas': 'canvas',
  'media.figcaption': 'figcaption',
  'media.figure': 'figure',
  'media.iframe': 'iframe',
  'media.img': 'img',
  'media.math': 'math',
  'media.picture': 'picture',
  'media.source': 'source',
  'media.svg': 'svg',
  'media.track': 'track',
  'media.video': 'video',
  'semantic.article': 'article',
  'semantic.aside': 'aside',
  'semantic.div': 'div',
  'semantic.footer': 'footer',
  'semantic.header': 'header',
  'semantic.main': 'main',
  'semantic.nav': 'nav',
  'semantic.section': 'section',
  'semantic.span': 'span',
  'table.caption': 'caption',
  'table.col': 'col',
  'table.colgroup': 'colgroup',
  'table.tbody': 'tbody',
  'table.td': 'td',
  'table.tfoot': 'tfoot',
  'table.th': 'th',
  'table.thead': 'thead',
  'table.tr': 'tr',
  text: 'span',
  'text.blockquote': 'blockquote',
  'text.br': 'br',
  'text.code': 'code',
  'text.em': 'em',
  'text.heading': 'h2',
  'text.h1': 'h1',
  'text.h2': 'h2',
  'text.h3': 'h3',
  'text.h4': 'h4',
  'text.h5': 'h5',
  'text.h6': 'h6',
  'text.hr': 'hr',
  'text.link': 'a',
  'text.mark': 'mark',
  'text.paragraph': 'p',
  'text.pre': 'pre',
  'text.quote': 'q',
  'text.small': 'small',
  'text.strong': 'strong',
  'text.time': 'time'
};

const allowedTags = new Set([
  'a', 'article', 'aside', 'blockquote', 'br', 'caption', 'code', 'dd', 'details', 'dialog', 'div', 'dl', 'dt', 'em',
  'figcaption', 'figure', 'footer', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'header', 'hr', 'iframe', 'img', 'li', 'main',
  'mark', 'math', 'menu', 'nav', 'ol', 'p', 'picture', 'pre', 'q', 'section', 'small', 'source', 'span', 'strong',
  'summary', 'table', 'tbody', 'td', 'tfoot', 'th', 'thead', 'time', 'track', 'tr', 'ul', 'audio', 'canvas', 'svg', 'video'
]);

export function hasStandardRuntimeRenderer(type: string): boolean {
  return type in standardTags;
}

export function renderStandardRuntime(context: RuntimeComponentRenderContext): ReactNode {
  const tag = standardTags[context.componentType] ?? 'div';
  if (tag === 'br' || tag === 'hr') return applyRuntimeNodePresentation(context, createElement(tag));
  const textComponent = context.componentType === 'text' || context.componentType.startsWith('text.');
  const children = context.children.length > 0
    ? context.children
    : textComponent
      ? String(context.props.text ?? '')
      : String(context.props.content ?? context.props.title ?? context.title);
  if (tag === 'a') {
    return applyRuntimeNodePresentation(context, <a href={resolveSafeRuntimeUrl(context.props.href, '#')} rel="noreferrer" target={String(context.props.target ?? '_self')}>{children}</a>);
  }
  if (tag === 'iframe') {
    return applyRuntimeNodePresentation(context, <iframe className="min-h-60 w-full rounded border border-slate-200" sandbox="allow-forms allow-same-origin" src={resolveSafeRuntimeUrl(context.props.src)} title={context.title} />);
  }
  if (tag === 'img') {
    return applyRuntimeNodePresentation(context, <img alt={context.title} className="max-w-full rounded" src={resolveSafeRuntimeUrl(context.props.src)} />);
  }
  if (tag === 'audio') return applyRuntimeNodePresentation(context, <audio className="w-full" controls src={resolveSafeRuntimeUrl(context.props.src)} />);
  if (tag === 'video') return applyRuntimeNodePresentation(context, <video className="max-h-96 w-full rounded bg-slate-950" controls src={resolveSafeRuntimeUrl(context.props.src)} />);
  if (tag === 'details') return applyRuntimeNodePresentation(context, <details className="rounded border border-slate-200 bg-white p-3"><summary>{context.title}</summary>{children}</details>);
  return applyRuntimeNodePresentation(context, createElement(allowedTags.has(tag) ? tag : 'div', {}, children));
}
